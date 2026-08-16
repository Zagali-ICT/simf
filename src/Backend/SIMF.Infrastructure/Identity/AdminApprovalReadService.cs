// Tests: SIMF.Api.Tests/PendingProfileReadTests.cs, AdminProfileReadTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using SIMF.Application.AccessControl.Abstractions;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Reads a pending-approval subject's profile for the CP's
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
    SimfAppDbContext appDbContext,
    IQrResolver qrResolver)
    : IAdminApprovalReadService
{
    /// <summary>One projected identity document. A named type rather than an
    /// anonymous one so the two projections below and the per-kind lookup they
    /// share can all name it; EF projects straight into the constructor, and the
    /// PII value converter decrypts <c>Number</c> on the way out exactly as it did
    /// when the number lived on the profile row.</summary>
    private sealed record IdentityDocumentRow(IdentityDocumentKind Kind, string Number);

    /// <summary>The registrant's document of one kind, or null when they hold
    /// none. This is what replaced the three <c>NationalId</c> /
    /// <c>IqamaNumber</c> / <c>PassportNumber</c> columns these projections used
    /// to read: the wire still carries all three keys, but they are now derived
    /// from the child rows rather than from three columns of their own.</summary>
    private static string? NumberOf(
        IReadOnlyList<IdentityDocumentRow>? documents, IdentityDocumentKind kind) =>
        documents?.FirstOrDefault(document => document.Kind == kind)?.Number;

    // Every non-admin account is UserType.Visitor. The
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
        // Canonicalise first. An offline badge arrives as a
        // ~61-character encrypted blob, not a QrId, so the direct lookup below
        // would miss it and report an unknown badge. A minted serial passes
        // through unchanged.
        var normalised = qrResolver.ToStoredQrId(qrId);

        var row = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.QrId == normalised)
            .Select(p => new
            {
                p.Id,
                p.UserId,
                p.QrId,
                p.Name,
                ProfileTypeName = p.ProfileType != null ? p.ProfileType.Name : null,
                ProfileTypeNameArabic = p.ProfileType != null ? p.ProfileType.NameArabic : null,
                ProfileTypeColor = p.ProfileType != null ? p.ProfileType.PageColor : null,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) { return null; }

        // The owner's email and self-chosen display name, when there IS an owner.
        // Most badges have none: a bulk order prints them long before anyone
        // claims one, so requiring an account here would have made the print-bag
        // station answer "no badge found" for every badge in the box.
        var user = row.UserId is { } registeredUserId
            ? await dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == registeredUserId)
                .Select(u => new { u.Email, u.DisplayName })
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return new AdminWalkInRegistrationResponse(
            row.UserId ?? Guid.Empty,
            user?.Email ?? string.Empty,
            // The account's display name when there is one; otherwise the name
            // the badge was actually printed from.
            user?.DisplayName ?? row.Name,
            row.QrId ?? string.Empty,
            row.ProfileTypeName ?? string.Empty,
            row.ProfileTypeNameArabic ?? string.Empty,
            row.ProfileTypeColor ?? "#244A77",
            row.Id);
    }

    /// <summary>Any-state full profile read scoped to the
    /// audience-vs-partner queue. Every non-admin account is
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
                u.AvatarFileId,
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
                Documents = p.IdentityDocuments
                    .Select(document => new IdentityDocumentRow(document.Kind, document.Number))
                    .ToList(),
                p.SaudiMobile,
                p.InternationalMobile,
                HasIdImage = p.IdImageFileId != null,
                InterestIds = p.Interests.Select(interest => interest.Id).ToList(),
                p.RejectionReason,
                p.RejectionReasonArabic,
                // Bi-Meeting rework — the two admin-assigned meeting-eligibility flags.
                p.AllowsSpeakerMeeting,
                p.AllowsDelegationMeeting,
            })
            .SingleOrDefaultAsync(cancellationToken);

        // Translate the logical FK Id back to the ISO code the
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
            NumberOf(profile?.Documents, IdentityDocumentKind.NationalId),
            NumberOf(profile?.Documents, IdentityDocumentKind.Iqama),
            NumberOf(profile?.Documents, IdentityDocumentKind.Passport),
            profile?.SaudiMobile,
            profile?.InternationalMobile,
            profile?.HasIdImage ?? false,
            // The avatar (profile photo) lives on SimfUser (Identity);
            // AvatarFileId is its StoredFile presence sentinel.
            user.AvatarFileId is not null,
            profile?.InterestIds ?? new List<Guid>(),
            profile?.RejectionReason,
            profile?.RejectionReasonArabic,
            user.CreatedAt,
            user.UpdatedAt,
            // Bi-Meeting rework — surface the two meeting-eligibility flags so the CP
            // edit form can pre-fill (and round-trip) the checkboxes.
            profile?.AllowsSpeakerMeeting ?? false,
            profile?.AllowsDelegationMeeting ?? false);
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
        // wrong-state id never produces a row. The scope guard
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
                u.AvatarFileId,
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
                Documents = p.IdentityDocuments
                    .Select(document => new IdentityDocumentRow(document.Kind, document.Number))
                    .ToList(),
                p.SaudiMobile,
                p.InternationalMobile,
                p.Gender,
                p.OrganisationId,
                Organisation = p.Organisation,
                p.PlateNumber,
                p.ReferenceNumber,
                HasIdImage = p.IdImageFileId != null,
                Interests = p.Interests.Select(interest => new { interest.Id, interest.Name, interest.NameArabic }).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        // Translate the logical FK Id back to the wire-side code.
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
            NumberOf(profile?.Documents, IdentityDocumentKind.NationalId),
            NumberOf(profile?.Documents, IdentityDocumentKind.Iqama),
            NumberOf(profile?.Documents, IdentityDocumentKind.Passport),
            profile?.SaudiMobile,
            profile?.InternationalMobile,
            profile?.HasIdImage ?? false,
            profile?.Interests.Select(i => i.Id).ToList() ?? new List<Guid>(),
            user.CreatedAt,
            (profile?.Gender ?? Gender.Unspecified).ToString(),
            profile?.OrganisationId,
            string.IsNullOrEmpty(profile?.Organisation?.Name) ? null : profile!.Organisation!.Name,
            string.IsNullOrEmpty(profile?.Organisation?.NameArabic) ? null : profile!.Organisation!.NameArabic,
            profile?.PlateNumber,
            profile?.ReferenceNumber,
            profile?.Interests.Select(i => new PendingProfileInterest(i.Name, i.NameArabic)).ToList(),
            // The avatar (profile photo) lives on SimfUser (Identity); its
            // AvatarFileId is the StoredFile pointer/presence sentinel.
            // Use IsNullOrEmpty to match every other presence reader.
            HasAvatar: user.AvatarFileId is not null);
    }

    // Country lookup helper. Cross-context (Country lives in
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

    // Audience-vs-partner scope guard. Audience scope (the
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
