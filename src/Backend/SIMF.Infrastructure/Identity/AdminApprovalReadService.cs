// Tests: SIMF.Api.Tests/PendingProfileReadTests.cs, AdminProfileReadTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-124 — Reads a pending-approval subject's profile for the CP's
/// "preview before approve" flow. Only emits a row when the subject
/// matches BOTH AccountState=PendingApproval AND the expected UserType
/// — the single 404-for-mismatch policy lives at the endpoint, this
/// service just returns null.
///
/// <para>The full user + profile + interest-id projection runs in one
/// EF query (left-join on UserProfile + the M-to-M interests collection)
/// so the call is cheap; admins typically open this modal once per
/// pending row.</para>
/// </summary>
internal sealed class AdminApprovalReadService(
    SimfIdentityDbContext dbContext,
    SimfAppDbContext appDbContext)
    : IAdminApprovalReadService
{
    // D-186: every non-admin account is UserType.Visitor. The
    // audience-vs-partner queue split routes on the linked
    // ProfileType.IsVisitor flag — true (or no profile yet) lands on
    // the Visitors queue, false lands on the Others queue.
    public Task<PendingProfileResponse?> GetPendingVisitorProfileAsync(
        Guid subjectUserId, CancellationToken cancellationToken = default) =>
        GetAsync(subjectUserId, expectAudienceScope: true, cancellationToken);

    public Task<PendingProfileResponse?> GetPendingOtherProfileAsync(
        Guid subjectUserId, CancellationToken cancellationToken = default) =>
        GetAsync(subjectUserId, expectAudienceScope: false, cancellationToken);

    public Task<AdminUserProfileView?> GetVisitorProfileAsync(
        Guid subjectUserId, CancellationToken cancellationToken = default) =>
        GetFullProfileAsync(subjectUserId, expectAudienceScope: true, cancellationToken);

    public Task<AdminUserProfileView?> GetOtherProfileAsync(
        Guid subjectUserId, CancellationToken cancellationToken = default) =>
        GetFullProfileAsync(subjectUserId, expectAudienceScope: false, cancellationToken);

    public async Task<AdminWalkInRegistrationResponse?> LookupByQrIdAsync(
        string qrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrId)) { return null; }
        var normalised = qrId.Trim().ToUpperInvariant();

        var row = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.QrId == normalised)
            .Select(p => new
            {
                p.UserId,
                p.QrId,
                ProfileTypeName = p.ProfileType != null ? p.ProfileType.Name : null,
                ProfileTypeNameArabic = p.ProfileType != null ? p.ProfileType.NameArabic : null,
                ProfileTypeColor = p.ProfileType != null ? p.ProfileType.PageColor : null,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) { return null; }

        // Pair the profile row with the owner so we can return the
        // badge-name + email the printable view renders.
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == row.UserId)
            .Select(u => new { u.Email, u.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) { return null; }

        return new AdminWalkInRegistrationResponse(
            row.UserId,
            user.Email ?? string.Empty,
            user.DisplayName,
            row.QrId ?? string.Empty,
            row.ProfileTypeName ?? string.Empty,
            row.ProfileTypeNameArabic ?? string.Empty,
            row.ProfileTypeColor ?? "#244A77");
    }

    /// <summary>D-126 — any-state full profile read scoped to the
    /// audience-vs-partner queue. D-186: every non-admin account is
    /// UserType.Visitor; the audience-vs-partner distinction is the
    /// linked <c>ProfileType.IsVisitor</c> flag (audience when true or
    /// when no profile type is set yet; partner when explicitly false).
    /// Keeps the scope-match guard so cross-queue enumeration still
    /// 404s.</summary>
    private async Task<AdminUserProfileView?> GetFullProfileAsync(
        Guid subjectUserId,
        bool expectAudienceScope,
        CancellationToken cancellationToken)
    {
        if (!await MatchesScopeAsync(subjectUserId, expectAudienceScope, cancellationToken))
        {
            return null;
        }
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == subjectUserId && u.UserType == UserType.Visitor)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.UserType,
                u.AccountState,
                u.CreatedAt,
                u.UpdatedAt,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) { return null; }

        var profile = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == subjectUserId)
            .Select(p => new
            {
                p.ProfileTypeId,
                ProfileType = p.ProfileType,
                p.QrId,
                p.NameArabic,
                p.Name,
                p.JobTitle,
                p.NationalityId,
                p.DateOfBirth,
                p.PlaceOfBirth,
                p.IsSaudi,
                p.NationalId,
                p.IqamaNumber,
                p.PassportNumber,
                p.SaudiMobile,
                p.InternationalMobile,
                HasIdImage = p.IdImageRelativePath != null,
                InterestIds = p.Interests.Select(interest => interest.Id).ToList(),
                p.RejectionReason,
                p.RejectionReasonArabic,
            })
            .SingleOrDefaultAsync(cancellationToken);

        // D-151 — translate the logical FK Id back to the ISO code the
        // wire contract exposes. Cross-context, so a separate query.
        var nationalityCode = profile is null
            ? null
            : await ResolveCodeAsync(profile.NationalityId, cancellationToken);

        return new AdminUserProfileView(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.UserType.ToString(),
            user.AccountState.ToString(),
            profile?.ProfileTypeId,
            profile?.ProfileType?.Name,
            profile?.ProfileType?.NameArabic,
            profile?.ProfileType?.PageColor,
            profile?.QrId,
            string.IsNullOrEmpty(profile?.NameArabic) ? null : profile.NameArabic,
            string.IsNullOrEmpty(profile?.Name) ? null : profile.Name,
            string.IsNullOrEmpty(profile?.JobTitle) ? null : profile.JobTitle,
            string.IsNullOrEmpty(nationalityCode) ? null : nationalityCode,
            profile?.DateOfBirth,
            string.IsNullOrEmpty(profile?.PlaceOfBirth) ? null : profile.PlaceOfBirth,
            profile?.IsSaudi ?? false,
            profile?.NationalId,
            profile?.IqamaNumber,
            profile?.PassportNumber,
            profile?.SaudiMobile,
            profile?.InternationalMobile,
            profile?.HasIdImage ?? false,
            profile?.InterestIds ?? new List<Guid>(),
            profile?.RejectionReason,
            profile?.RejectionReasonArabic,
            user.CreatedAt,
            user.UpdatedAt);
    }

    private async Task<PendingProfileResponse?> GetAsync(
        Guid subjectUserId,
        bool expectAudienceScope,
        CancellationToken cancellationToken)
    {
        if (!await MatchesScopeAsync(subjectUserId, expectAudienceScope, cancellationToken))
        {
            return null;
        }
        // The single guarded query — both the state AND the type are
        // filtered before any projection so a wrong-type or
        // wrong-state id never produces a row. D-186: scope guard
        // (audience vs partner) is enforced upstream by
        // MatchesScopeAsync over the linked ProfileType.IsVisitor.
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == subjectUserId
                && u.AccountState == AccountState.PendingApproval
                && u.UserType == UserType.Visitor)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.UserType,
                u.CreatedAt,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) { return null; }

        // Profile + interest ids — left-join because an admin-created
        // user may be approved before the user fills the profile form
        // (the form is self-service). When no profile row exists we
        // still return the response with nulls + an empty Interests
        // list so the modal can render "not filled yet".
        var profile = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == subjectUserId)
            .Select(p => new
            {
                p.ProfileTypeId,
                ProfileType = p.ProfileType,
                p.NameArabic,
                p.Name,
                p.JobTitle,
                p.NationalityId,
                p.DateOfBirth,
                p.PlaceOfBirth,
                p.IsSaudi,
                p.NationalId,
                p.IqamaNumber,
                p.PassportNumber,
                p.SaudiMobile,
                p.InternationalMobile,
                HasIdImage = p.IdImageRelativePath != null,
                InterestIds = p.Interests.Select(interest => interest.Id).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        // D-151 — translate the logical FK Id back to the wire-side code.
        var nationalityCode = profile is null
            ? null
            : await ResolveCodeAsync(profile.NationalityId, cancellationToken);

        return new PendingProfileResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.UserType.ToString(),
            profile?.ProfileTypeId,
            profile?.ProfileType?.Name,
            profile?.ProfileType?.NameArabic,
            string.IsNullOrEmpty(profile?.NameArabic) ? null : profile.NameArabic,
            string.IsNullOrEmpty(profile?.Name) ? null : profile.Name,
            string.IsNullOrEmpty(profile?.JobTitle) ? null : profile.JobTitle,
            string.IsNullOrEmpty(nationalityCode) ? null : nationalityCode,
            profile?.DateOfBirth,
            string.IsNullOrEmpty(profile?.PlaceOfBirth) ? null : profile.PlaceOfBirth,
            profile?.IsSaudi ?? false,
            profile?.NationalId,
            profile?.IqamaNumber,
            profile?.PassportNumber,
            profile?.SaudiMobile,
            profile?.InternationalMobile,
            profile?.HasIdImage ?? false,
            profile?.InterestIds ?? new List<Guid>(),
            user.CreatedAt);
    }

    // D-151 — Country lookup helper. Cross-context (Country lives in
    // SimfAppDbContext, the profile in SimfIdentityDbContext) so this
    // is a separate cheap single-row index query.
    private async Task<string> ResolveCodeAsync(int id, CancellationToken cancellationToken)
    {
        if (id == 0) { return string.Empty; }
        return await appDbContext.Countries
            .AsNoTracking()
            .Where(country => country.Id == id)
            .Select(country => country.Code)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
    }

    // D-186 — audience-vs-partner scope guard. Audience scope (the
    // visitors queue) accepts a user with no profile type yet OR a
    // profile type whose IsVisitor flag is true. Partner scope (the
    // others queue) requires an explicitly false IsVisitor — a user
    // with no profile type is not a partner. Cross-context: the
    // ProfileType row lives in the App DB so the lookup is two
    // single-row queries (cheap, and the page typically opens once
    // per row).
    private async Task<bool> MatchesScopeAsync(
        Guid subjectUserId,
        bool expectAudienceScope,
        CancellationToken cancellationToken)
    {
        var profileTypeId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == subjectUserId)
            .Select(p => p.ProfileTypeId)
            .SingleOrDefaultAsync(cancellationToken);

        if (profileTypeId is null)
        {
            // No profile yet → audience-side by default; partner queue
            // rejects this id with the usual 404.
            return expectAudienceScope;
        }

        var isVisitor = await appDbContext.ProfileTypes
            .AsNoTracking()
            .Where(p => p.Id == profileTypeId)
            .Select(p => (bool?)p.IsForVisitor)
            .SingleOrDefaultAsync(cancellationToken);
        if (isVisitor is null)
        {
            return expectAudienceScope;
        }

        return isVisitor.Value == expectAudienceScope;
    }
}
