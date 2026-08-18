// Tests: SIMF.Api.Tests/AdminProfileTypeTests.cs, SIMF.Api.Tests/GridContractTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Admin CRUD over the <c>ProfileTypes</c> table. Read-only
/// listings are still served by <see cref="AdminProfileTypeQueryService"/>;
/// every mutation here audits one row and respects the per-UserType name
/// uniqueness rule.
/// </summary>
internal sealed class AdminProfileTypeCommandService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminProfileTypeCommandService> logger) : IAdminProfileTypeCommandService
{
    /// <summary>
    /// The grid contract for /admin/profile-types: one entry per key the Visitor
    /// and Other profile-type pages can send. <c>isVisitor</c> is the audience /
    /// partner split those two pages pin; <c>createdAt</c> was previously sortable
    /// ascending only, so a descending click silently returned alphabetical order.
    /// </summary>
    private static readonly GridColumns<UserProfileType> Columns = new GridColumns<UserProfileType>()
        .Add("name", profileType => profileType.Name, searchable: true)
        .Add("nameArabic", profileType => profileType.NameArabic, searchable: true)
        .Add("isActive", profileType => profileType.IsActive)
        .Add("isVisitor", profileType => profileType.IsForVisitor)
        .Add("createdAt", profileType => profileType.CreatedAt)
        // The scope key stays in the allow-list even though the two CP pages no
        // longer send it. Every non-admin profile type lives under Visitor, so the
        // old service could ignore the key entirely and still look right; dropping
        // it now would turn a request that has always worked into a 400 for
        // anything still sending it. Honouring it makes the answer correct rather
        // than accidental.
        .AddFilter("userType", raw =>
        {
            if (!Enum.TryParse<UserType>(raw, ignoreCase: true, out var scope))
            {
                throw GridFilters.ValueInvalid(
                    "userType", raw, $"one of: {string.Join(", ", Enum.GetNames<UserType>())}");
            }

            return scope == UserType.Visitor
                ? profileType => true
                : profileType => false;
        })
        .DefaultOrder("name")
        .PageSize(fallback: 25, max: 200);

    /// <summary>The row shape, as SQL: what the list projects.</summary>
    private static readonly Expression<Func<UserProfileType, AdminProfileTypeSummary>> ToSummary =
        profileType => new AdminProfileTypeSummary(
            profileType.Id,
            profileType.Name,
            profileType.NameArabic,
            profileType.PageColor,
            nameof(UserType.Visitor),
            profileType.MobileAppRole.ToString(),
            profileType.IsActive,
            profileType.IsForVisitor,
            profileType.IsAppRegisterable,
            profileType.ShowInPartnerDirectory,
            profileType.IsVipTier);

    /// <summary>The same row shape over an already-materialised entity, for the
    /// single-row read and for the two writers that return the row they just
    /// saved. Compiled from the expression above rather than written out a second
    /// time, so the two forms cannot drift apart.</summary>
    private static readonly Func<UserProfileType, AdminProfileTypeSummary> Summarise =
        ToSummary.Compile();

    public Task<GridPage<AdminProfileTypeSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        dbContext.ProfileTypes.ToGridPageAsync(
            query, Columns, profileType => profileType.Id, ToSummary, cancellationToken);

    public async Task<AdminProfileTypeSummary?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var profileType = await dbContext.ProfileTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return profileType is null
            ? null
            : Summarise(profileType);
    }

    public async Task<AdminProfileTypeSummary> CreateAsync(
        Guid actorUserId,
        AdminCreateProfileTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        // Only the Visitor scope is accepted for non-admin
        // profile types; the audience-vs-partner split lives on
        // request.IsVisitor (true = audience, false = partner / staff).
        if (!Enum.TryParse<UserType>(request.UserType, ignoreCase: true, out var userType)
            || userType != UserType.Visitor)
        {
            throw new ApiException(
                ErrorCodes.ProfileTypeInvalidUserType, 400,
                "A profile type may only be created for the Visitor scope.",
                "لا يمكن إنشاء نوع ملف شخصي إلا ضمن نطاق الزائر.");
        }

        var name = (request.Name ?? string.Empty).Trim();
        var nameArabic = (request.NameArabic ?? string.Empty).Trim();
        var pageColor = (request.PageColor ?? string.Empty).Trim();

        // Per-UserType name uniqueness (case-insensitive — SQL Server's
        // default collation handles the comparison without ToLower).
        var clash = await dbContext.ProfileTypes
            .AsNoTracking()
            .AnyAsync(
                row => row.Name == name,
                cancellationToken);
        if (clash)
        {
            throw new ApiException(
                ErrorCodes.ProfileTypeNameTaken, 409,
                $"A profile type named '{name}' already exists for {userType}.",
                $"يوجد نوع ملف شخصي بالاسم '{name}' لـ {userType} بالفعل.");
        }

        var mobileAppRole = ParseMobileAppRole(request.MobileAppRole);

        var now = timeProvider.SimfNow();
        var profileType = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameArabic = nameArabic,
            PageColor = pageColor,
            // IsVisitor drives CP queue routing — true = Visitors
            // approval queue, false = Others approval queue.
            IsForVisitor = request.IsVisitor,
            MobileAppRole = mobileAppRole,
            // App sign-up picker visibility (default true; the CP
            // form sends false for CP-only operational types).
            IsAppRegisterable = request.IsAppRegisterable,
            // Meet-People networking visibility (default true).
            ShowInPartnerDirectory = request.ShowInPartnerDirectory,
            // VIP audience tier — VIP-tier seat self-reservation + the app's
            // isVip. Default false; not a meetings gate. Until this was bound
            // the seeder was the ONLY writer, so a CP-created type could never
            // be VIP.
            IsVipTier = request.IsVipTier,
            IsActive = request.IsActive,
            CreatedAt = now,
            // The small number the printed badge carries. Allocated, not
            // entered: it is a wire detail of the QR, not something an
            // administrator should have to pick or keep unique.
            Code = await ProfileTypeCodeAllocator.NextAsync(dbContext, cancellationToken),
        };
        dbContext.ProfileTypes.Add(profileType);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        // ProfileTypeCodeAllocator reads MAX(Code) and adds one, which is
        // a read-then-insert: two admins creating a type at the same instant both
        // compute the same number and the filtered unique index refuses the
        // loser. The index is doing its job - a reused code would make a retired
        // badge read as a different type on a scanner that is offline and cannot
        // check the database - but an unhandled DbUpdateException surfaced as a
        // 500. Translated to a typed 409, mirroring how the same changeset
        // already handles the QrId race (AdminAccountService). Every other
        // DbUpdateException still rethrows byte-identically.
        catch (DbUpdateException ex) when (ex.ViolatesAnyIndex("IX_ProfileTypes_Code"))
        {
            throw new ApiException(
                ErrorCodes.ProfileTypeCodeRace, 409,
                "Another profile type was created at the same moment. Try again.",
                "تم إنشاء نوع ملف آخر في نفس اللحظة. حاول مرة أخرى.");
        }

        await auditLog.WriteSuccessAsync(
            AuditEvents.ProfileTypeCreated,
            actorUserId,
            $"id={profileType.Id}; userType={userType}; name={name}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created ProfileType {Name} ({Id}) for {UserType}",
            actorUserId, name, profileType.Id, userType);

        return Summarise(profileType);
    }

    public async Task<AdminProfileTypeSummary> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateProfileTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var profileType = await dbContext.ProfileTypes
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ProfileTypeNotFound, 404,
                "The profile type was not found.",
                "لم يتم العثور على نوع الملف الشخصي.");

        var name = (request.Name ?? string.Empty).Trim();

        if (!string.Equals(profileType.Name, name, StringComparison.Ordinal))
        {
            var clash = await dbContext.ProfileTypes
                .AsNoTracking()
                .AnyAsync(
                    row => row.Id != id
                        && row.Name == name,
                    cancellationToken);
            if (clash)
            {
                throw new ApiException(
                    ErrorCodes.ProfileTypeNameTaken, 409,
                    $"A profile type named '{name}' already exists.",
                    $"يوجد نوع ملف شخصي بالاسم '{name}' بالفعل.");
            }
        }

        // Capture the prior
        // IsVisitor BEFORE the mutation so the audit Detail records
        // any flip. Silent flips would let an insider mass-launder
        // partner accounts into the audience queue with no SOC trail.
        var oldIsVisitor = profileType.IsForVisitor;

        profileType.Name = name;
        profileType.NameArabic = (request.NameArabic ?? string.Empty).Trim();
        profileType.PageColor = (request.PageColor ?? string.Empty).Trim();
        profileType.MobileAppRole = ParseMobileAppRole(request.MobileAppRole);
        profileType.IsActive = request.IsActive;
        // IsVisitor is mutable — flipping it re-routes the row
        // between the CP Visitors and Others approval queues. The
        // underlying user accounts already use UserType.Visitor either way.
        profileType.IsForVisitor = request.IsVisitor;
        // App sign-up picker visibility — the admin toggles whether a
        // self-registering user may pick this type.
        profileType.IsAppRegisterable = request.IsAppRegisterable;
        // Meet-People networking visibility.
        profileType.ShowInPartnerDirectory = request.ShowInPartnerDirectory;
        // VIP audience tier. Assigned unconditionally like the flags above, so
        // an edit that clears the checkbox actually clears it.
        profileType.IsVipTier = request.IsVipTier;
        profileType.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        var isVisitorChanged = oldIsVisitor != profileType.IsForVisitor;
        // Count linked accounts when the flag
        // changed so SOC can prioritise the audit row (a flip on a
        // ProfileType with hundreds of linked accounts is a much
        // larger blast radius than a flip on a freshly-created one).
        var linkedAccountCount = isVisitorChanged
            ? await dbContext.UserProfiles.AsNoTracking()
                .CountAsync(profile => profile.ProfileTypeId == id, cancellationToken)
            : 0;

        await auditLog.WriteSuccessAsync(
            AuditEvents.ProfileTypeUpdated,
            actorUserId,
            isVisitorChanged
                ? $"id={profileType.Id}; name={profileType.Name}; "
                    + $"active={profileType.IsActive}; "
                    + $"isVisitorChanged=true; isVisitorOld={oldIsVisitor}; "
                    + $"isVisitorNew={profileType.IsForVisitor}; "
                    + $"linkedAccountCount={linkedAccountCount}"
                : $"id={profileType.Id}; name={profileType.Name}; "
                    + $"active={profileType.IsActive}; isVisitorChanged=false",
            cancellationToken);

        return Summarise(profileType);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var profileType = await dbContext.ProfileTypes
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ProfileTypeNotFound, 404,
                "The profile type was not found.",
                "لم يتم العثور على نوع الملف الشخصي.");

        // Refuse the delete if any UserProfile still references this id —
        // protects the FK invariant. The CP can still soft-delete it later
        // after re-assigning the affected profiles.
        var inUse = await dbContext.UserProfiles
            .AsNoTracking()
            .AnyAsync(profile => profile.ProfileTypeId == id, cancellationToken);
        if (inUse)
        {
            throw new ApiException(
                ErrorCodes.ProfileTypeInUse, 409,
                "The profile type cannot be removed while it is still assigned to one or more accounts.",
                "لا يمكن إزالة نوع الملف الشخصي طالما لا يزال مُسنداً إلى حساب واحد أو أكثر.");
        }

        if (!profileType.IsActive)
        {
            return; // idempotent — already deactivated.
        }

        profileType.IsActive = false;
        profileType.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ProfileTypeDeactivated,
            actorUserId,
            $"id={profileType.Id}; name={profileType.Name}",
            cancellationToken);
    }

    /// <summary>Parses the wire-side stringly mobile-app-role,
    /// rejecting unknown values with a typed 400. Null / empty defaults
    /// to <see cref="MobileAppRole.None"/>. <see cref="MobileAppRole.Visitor"/>
    /// is rejected because the Visitor mapping is resolved from
    /// <c>UserType</c> at JWT issue time, never from a ProfileType row.</summary>
    private static MobileAppRole ParseMobileAppRole(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return MobileAppRole.None;
        }
        if (!Enum.TryParse<MobileAppRole>(raw, ignoreCase: true, out var parsed))
        {
            throw new ApiException(
                ErrorCodes.ProfileTypeInvalidUserType, 400,
                $"'{raw}' is not a valid mobile-app role.",
                $"قيمة '{raw}' ليست دوراً صالحاً في تطبيق الجوّال.");
        }
        if (parsed == MobileAppRole.Visitor)
        {
            throw new ApiException(
                ErrorCodes.ProfileTypeInvalidUserType, 400,
                "MobileAppRole.Visitor is resolved from UserType, not from a profile type row.",
                "يُحدَّد دور الزائر من نوع المستخدم، لا من نوع الملف الشخصي.");
        }
        return parsed;
    }
}
