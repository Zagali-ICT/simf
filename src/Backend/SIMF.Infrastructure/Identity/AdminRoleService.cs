// Tests: SIMF.Api.Tests/AdminRolesTests.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-134 Sprint A — admin CRUD over <c>SimfRole</c>. Built on the existing
/// Identity infrastructure (uses <see cref="RoleManager{TRole}"/> so the
/// stamp + normalised-name invariants stay correct), the existing
/// <c>RolePermission</c> join (read for the per-role permission count),
/// and the existing <c>IUserRoleStore</c> (read for the per-role user
/// count). **No schema change** — the entities and tables existed since
/// the InitialCreate migrations.
/// </summary>
internal sealed class AdminRoleService(
    SimfIdentityDbContext dbContext,
    RoleManager<SimfRole> roleManager,
    IAuditLog auditLog,
    ILogger<AdminRoleService> logger) : IAdminRoleService
{
    public async Task<GridPage<AdminRoleSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var roles = dbContext.Roles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            roles = roles.Where(role => EF.Functions.Like(role.Name!, $"%{term}%"));
        }

        roles = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", true) => roles.OrderByDescending(role => role.Name),
            ("name", false) => roles.OrderBy(role => role.Name),
            ("baseline", true) => roles.OrderByDescending(role => role.IsBaseline)
                                       .ThenBy(role => role.Name),
            ("baseline", false) => roles.OrderBy(role => role.IsBaseline)
                                        .ThenBy(role => role.Name),
            _ => roles.OrderByDescending(role => role.IsBaseline)
                      .ThenBy(role => role.Name),
        };

        var total = await roles.CountAsync(cancellationToken);

        // Project to summary; the per-role UserCount + PermissionCount
        // are computed inline so the round-trip is a single query.
        var page = await roles
            .Skip(skip)
            .Take(top)
            .Select(role => new AdminRoleSummary(
                role.Id,
                role.Name ?? string.Empty,
                role.IsBaseline,
                dbContext.UserRoles.Count(userRole => userRole.RoleId == role.Id),
                dbContext.RolePermissions.Count(rp => rp.RoleId == role.Id)))
            .ToListAsync(cancellationToken);

        return GridPage<AdminRoleSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminRoleSummary?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var summary = await dbContext.Roles
            .AsNoTracking()
            .Where(role => role.Id == id)
            .Select(role => new AdminRoleSummary(
                role.Id,
                role.Name ?? string.Empty,
                role.IsBaseline,
                dbContext.UserRoles.Count(userRole => userRole.RoleId == role.Id),
                dbContext.RolePermissions.Count(rp => rp.RoleId == role.Id)))
            .SingleOrDefaultAsync(cancellationToken);
        return summary;
    }

    public async Task<AdminRoleSummary> CreateAsync(
        Guid actorUserId,
        AdminCreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length is < 1 or > 64)
        {
            throw new ApiException(
                ErrorCodes.RoleInvalid, 400,
                "The role name must be between 1 and 64 characters.",
                "يجب أن يتراوح طول اسم الدور بين 1 و 64 حرفاً.");
        }

        if (await roleManager.RoleExistsAsync(name))
        {
            throw new ApiException(
                ErrorCodes.RoleNameDuplicate, 409,
                $"A role named '{name}' already exists.",
                $"يوجد دور بالاسم '{name}' بالفعل.");
        }

        var role = new SimfRole
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsBaseline = false,
        };

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            // RoleManager surfaces concurrency / DB issues here; bubble as
            // a generic 400 so the caller sees the message.
            var description = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new ApiException(
                ErrorCodes.RoleInvalid, 400,
                $"The role could not be created: {description}.",
                $"تعذّر إنشاء الدور: {description}.");
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.RoleCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={role.Id}; name={name}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created Role {Name} ({Id})",
            actorUserId, name, role.Id);

        return new AdminRoleSummary(role.Id, name, false, 0, 0);
    }

    public async Task<AdminRoleSummary> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(id.ToString())
            ?? throw new ApiException(
                ErrorCodes.RoleNotFound, 404,
                "The role was not found.",
                "لم يتم العثور على الدور.");

        if (role.IsBaseline)
        {
            throw new ApiException(
                ErrorCodes.RoleIsBaseline, 409,
                "Baseline roles cannot be renamed.",
                "لا يمكن إعادة تسمية الأدوار الأساسية.");
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length is < 1 or > 64)
        {
            throw new ApiException(
                ErrorCodes.RoleInvalid, 400,
                "The role name must be between 1 and 64 characters.",
                "يجب أن يتراوح طول اسم الدور بين 1 و 64 حرفاً.");
        }

        if (!string.Equals(role.Name, name, StringComparison.Ordinal))
        {
            // Check the normalised-name clash against any OTHER role —
            // RoleManager.SetNameAsync also re-normalises but we want a
            // friendly bilingual error before the underlying SQL fault.
            var clashing = await roleManager.FindByNameAsync(name);
            if (clashing is not null && clashing.Id != role.Id)
            {
                throw new ApiException(
                    ErrorCodes.RoleNameDuplicate, 409,
                    $"A role named '{name}' already exists.",
                    $"يوجد دور بالاسم '{name}' بالفعل.");
            }
            await roleManager.SetRoleNameAsync(role, name);
        }

        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            var description = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new ApiException(
                ErrorCodes.RoleInvalid, 400,
                $"The role could not be updated: {description}.",
                $"تعذّر تحديث الدور: {description}.");
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.RoleUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={role.Id}; name={name}",
        }, cancellationToken);

        var userCount = await dbContext.UserRoles
            .AsNoTracking()
            .CountAsync(userRole => userRole.RoleId == role.Id, cancellationToken);
        var permissionCount = await dbContext.RolePermissions
            .AsNoTracking()
            .CountAsync(rp => rp.RoleId == role.Id, cancellationToken);

        return new AdminRoleSummary(role.Id, name, false, userCount, permissionCount);
    }

    public async Task DeleteAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(id.ToString())
            ?? throw new ApiException(
                ErrorCodes.RoleNotFound, 404,
                "The role was not found.",
                "لم يتم العثور على الدور.");

        if (role.IsBaseline)
        {
            throw new ApiException(
                ErrorCodes.RoleIsBaseline, 409,
                "Baseline roles cannot be deleted.",
                "لا يمكن حذف الأدوار الأساسية.");
        }

        // No user must currently hold the role — the CP needs to unassign
        // first. Surfaces RoleInUse so the toast can guide the operator.
        var holders = await dbContext.UserRoles
            .AsNoTracking()
            .CountAsync(userRole => userRole.RoleId == role.Id, cancellationToken);
        if (holders > 0)
        {
            throw new ApiException(
                ErrorCodes.RoleInUse, 409,
                $"The role cannot be deleted while {holders} user(s) hold it.",
                $"لا يمكن حذف الدور طالما يحمله {holders} مستخدم(مستخدمين).");
        }

        // Cascade-delete the role's RolePermission rows first; the FK does
        // not auto-cascade in the SIMF schema.
        var grants = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync(cancellationToken);
        if (grants.Count > 0)
        {
            dbContext.RolePermissions.RemoveRange(grants);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            var description = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new ApiException(
                ErrorCodes.RoleInvalid, 400,
                $"The role could not be deleted: {description}.",
                $"تعذّر حذف الدور: {description}.");
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.RoleDeleted,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={role.Id}; name={role.Name}",
        }, cancellationToken);
    }

    public async Task<AdminRolePermissionsResponse?> GetPermissionsAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var role = await dbContext.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return null;
        }

        var grantedCodes = await (
            from rolePermission in dbContext.RolePermissions
            join permission in dbContext.Permissions
                on rolePermission.PermissionId equals permission.Id
            where rolePermission.RoleId == id
            select permission.Code)
            .ToListAsync(cancellationToken);

        return new AdminRolePermissionsResponse(
            role.Id, role.Name ?? string.Empty, role.IsBaseline, grantedCodes);
    }

    public async Task SetPermissionsAsync(
        Guid actorUserId,
        Guid id,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(id.ToString())
            ?? throw new ApiException(
                ErrorCodes.RoleNotFound, 404,
                "The role was not found.",
                "لم يتم العثور على الدور.");

        if (role.IsBaseline)
        {
            // Administrator's permissions are the wildcard; GateOperator and
            // PublicRelations are seeded. Baseline grants are not hand-editable.
            throw new ApiException(
                ErrorCodes.RoleIsBaseline, 409,
                "Baseline roles' permissions cannot be edited.",
                "لا يمكن تعديل صلاحيات الأدوار الأساسية.");
        }

        var requested = codes.Distinct(StringComparer.Ordinal).ToList();
        var validCodes = PermissionCatalog.All
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);
        var unknown = requested.Where(code => !validCodes.Contains(code)).ToList();
        if (unknown.Count > 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                $"Unknown permission code(s): {string.Join(", ", unknown)}.",
                "رمز صلاحية واحد أو أكثر غير معروف.");
        }

        // Resolve the requested codes to their seeded Permission ids.
        var requestedPermissions = await dbContext.Permissions
            .Where(permission => requested.Contains(permission.Code))
            .Select(permission => permission.Id)
            .ToListAsync(cancellationToken);

        // Apply the difference so an already-granted permission is not deleted
        // and re-inserted in the same unit of work (that would trip the EF
        // change tracker on the composite key).
        var existing = await dbContext.RolePermissions
            .Where(rolePermission => rolePermission.RoleId == id)
            .ToListAsync(cancellationToken);
        var existingIds = existing.Select(rolePermission => rolePermission.PermissionId).ToHashSet();
        var requestedIds = requestedPermissions.ToHashSet();

        var toRemove = existing
            .Where(rolePermission => !requestedIds.Contains(rolePermission.PermissionId))
            .ToList();
        var toAdd = requestedPermissions
            .Where(permissionId => !existingIds.Contains(permissionId))
            .ToList();

        if (toRemove.Count > 0)
        {
            dbContext.RolePermissions.RemoveRange(toRemove);
        }
        foreach (var permissionId in toAdd)
        {
            dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = id,
                PermissionId = permissionId,
            });
        }
        if (toRemove.Count > 0 || toAdd.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.RolePermissionsUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; granted={requestedPermissions.Count}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} set {Count} permission(s) on role {RoleId}.",
            actorUserId, requestedPermissions.Count, id);
    }
}
