// Tests: SIMF.Api.Tests/AdminProfileTypeTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-115 — admin CRUD over the <c>ProfileTypes</c> table. Read-only
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
    public async Task<GridPage<AdminProfileTypeSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var rows = dbContext.ProfileTypes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(profileType =>
                EF.Functions.Like(profileType.Name, $"%{term}%")
                || EF.Functions.Like(profileType.NameArabic, $"%{term}%"));
        }

        if (query.Filters.TryGetValue("name", out var nameFilter)
            && !string.IsNullOrWhiteSpace(nameFilter))
        {
            rows = rows.Where(profileType =>
                EF.Functions.Like(profileType.Name, $"%{nameFilter}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(profileType => profileType.IsActive == isActive);
        }
        // D-186: the CP Other-profile-types page filters this server-side.
        if (query.Filters.TryGetValue("isVisitor", out var isVisitorFilter)
            && bool.TryParse(isVisitorFilter, out var isVisitorValue))
        {
            rows = rows.Where(profileType => profileType.IsForVisitor == isVisitorValue);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", true) => rows.OrderByDescending(profileType => profileType.Name),
            ("name", false) => rows.OrderBy(profileType => profileType.Name),
            ("namearabic", true) => rows.OrderByDescending(profileType => profileType.NameArabic),
            ("namearabic", false) => rows.OrderBy(profileType => profileType.NameArabic),
            ("createdat", false) => rows.OrderBy(profileType => profileType.CreatedAt),
            _ => rows.OrderBy(profileType => profileType.Name),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(profileType => new AdminProfileTypeSummary(
                profileType.Id,
                profileType.Name,
                profileType.NameArabic,
                profileType.PageColor,
                nameof(UserType.Visitor),
                profileType.MobileAppRole.ToString(),
                profileType.IsActive,
                profileType.IsForVisitor,
                profileType.IsAppRegisterable,
                profileType.ShowInPartnerDirectory))
            .ToListAsync(cancellationToken);

        return GridPage<AdminProfileTypeSummary>.Of(page, total,
            skip, top);
    }

    public async Task<AdminProfileTypeSummary?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var profileType = await dbContext.ProfileTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return profileType is null
            ? null
            : ToSummary(profileType);
    }

    public async Task<AdminProfileTypeSummary> CreateAsync(
        Guid actorUserId,
        AdminCreateProfileTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        // D-186: only the Visitor scope is accepted for non-admin
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

        var now = timeProvider.GetUtcNow();
        var profileType = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameArabic = nameArabic,
            PageColor = pageColor,
            // D-186: IsVisitor drives CP queue routing — true = Visitors
            // approval queue, false = Others approval queue.
            IsForVisitor = request.IsVisitor,
            MobileAppRole = mobileAppRole,
            // D-725: app sign-up picker visibility (default true; the CP
            // form sends false for CP-only operational types).
            IsAppRegisterable = request.IsAppRegisterable,
            // D-760: Meet-People networking visibility (default true).
            ShowInPartnerDirectory = request.ShowInPartnerDirectory,
            IsActive = request.IsActive,
            CreatedAt = now,
            // D-809 — the small number the printed badge carries. Allocated, not
            // entered: it is a wire detail of the QR, not something an
            // administrator should have to pick or keep unique.
            Code = await ProfileTypeCodeAllocator.NextAsync(dbContext, cancellationToken),
        };
        dbContext.ProfileTypes.Add(profileType);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ProfileTypeCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={profileType.Id}; userType={userType}; name={name}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created ProfileType {Name} ({Id}) for {UserType}",
            actorUserId, name, profileType.Id, userType);

        return ToSummary(profileType);
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

        // D-186 review-pass (threat-detection H-1): capture the prior
        // IsVisitor BEFORE the mutation so the audit Detail records
        // any flip. Silent flips would let an insider mass-launder
        // partner accounts into the audience queue with no SOC trail.
        var oldIsVisitor = profileType.IsForVisitor;

        profileType.Name = name;
        profileType.NameArabic = (request.NameArabic ?? string.Empty).Trim();
        profileType.PageColor = (request.PageColor ?? string.Empty).Trim();
        profileType.MobileAppRole = ParseMobileAppRole(request.MobileAppRole);
        profileType.IsActive = request.IsActive;
        // D-186: IsVisitor is mutable — flipping it re-routes the row
        // between the CP Visitors and Others approval queues. The
        // underlying user accounts already use UserType.Visitor either way.
        profileType.IsForVisitor = request.IsVisitor;
        // D-725: app sign-up picker visibility — the admin toggles whether a
        // self-registering user may pick this type.
        profileType.IsAppRegisterable = request.IsAppRegisterable;
        // D-760: Meet-People networking visibility.
        profileType.ShowInPartnerDirectory = request.ShowInPartnerDirectory;
        profileType.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        var isVisitorChanged = oldIsVisitor != profileType.IsForVisitor;
        // D-186 review-pass: count linked accounts when the flag
        // changed so SOC can prioritise the audit row (a flip on a
        // ProfileType with hundreds of linked accounts is a much
        // larger blast radius than a flip on a freshly-created one).
        var linkedAccountCount = isVisitorChanged
            ? await dbContext.UserProfiles.AsNoTracking()
                .CountAsync(profile => profile.ProfileTypeId == id, cancellationToken)
            : 0;

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ProfileTypeUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = isVisitorChanged
                ? $"id={profileType.Id}; name={profileType.Name}; "
                    + $"active={profileType.IsActive}; "
                    + $"isVisitorChanged=true; isVisitorOld={oldIsVisitor}; "
                    + $"isVisitorNew={profileType.IsForVisitor}; "
                    + $"linkedAccountCount={linkedAccountCount}"
                : $"id={profileType.Id}; name={profileType.Name}; "
                    + $"active={profileType.IsActive}; isVisitorChanged=false",
        }, cancellationToken);

        return ToSummary(profileType);
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
        profileType.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ProfileTypeDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={profileType.Id}; name={profileType.Name}",
        }, cancellationToken);
    }

    private static AdminProfileTypeSummary ToSummary(UserProfileType profileType) =>
        new(profileType.Id,
            profileType.Name,
            profileType.NameArabic,
            profileType.PageColor,
            nameof(UserType.Visitor),
            profileType.MobileAppRole.ToString(),
            profileType.IsActive,
            profileType.IsForVisitor,
            profileType.IsAppRegisterable,
            profileType.ShowInPartnerDirectory);

    /// <summary>D-161 — parses the wire-side stringly mobile-app-role,
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
