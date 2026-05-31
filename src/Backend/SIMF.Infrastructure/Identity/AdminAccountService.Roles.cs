using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-209 (A2 split): the Issue-1 RBAC role-assignment surface of
/// <see cref="AdminAccountService"/> — read + diff-apply an admin user's
/// roles, with the last-administrator lockout guard. Split into its own
/// partial-class file for navigability; behaviour and DI are unchanged
/// (still one scoped <c>AdminAccountService</c> instance).
/// </summary>
internal sealed partial class AdminAccountService
{
    // -- Issue-1 — RBAC role assignment for an existing admin user ----------

    public async Task<AdminUserRolesResponse> GetUserRolesAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByIdAsync(userId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The user was not found.",
                "لم يتم العثور على المستخدم.");

        var roles = await accounts.GetRolesAsync(user, cancellationToken);
        return new AdminUserRolesResponse(userId, roles.ToList());
    }

    public async Task SetUserRolesAsync(
        Guid actorUserId,
        Guid userId,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByIdAsync(userId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The user was not found.",
                "لم يتم العثور على المستخدم.");

        // RBAC roles apply only to Admin-typed users (D-048).
        if (user.UserType != UserType.Admin)
        {
            throw new ApiException(
                ErrorCodes.AdminRolesTargetNotAdmin, 409,
                "Only admin accounts can hold roles.",
                "يمكن للحسابات الإدارية فقط حمل الأدوار.");
        }

        var requested = roleNames
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var roleName in requested)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                throw new ApiException(
                    ErrorCodes.RoleNotFound, 400,
                    $"The role '{roleName}' does not exist.",
                    $"الدور '{roleName}' غير موجود.");
            }
        }

        var current = await accounts.GetRolesAsync(user, cancellationToken);
        var toRemove = current.Where(role => !requested.Contains(role)).ToList();
        var toAdd = requested.Where(role => !current.Contains(role)).ToList();

        // Lockout guard — never strip the Administrator role from the last
        // administrator (that would leave the system with no one who can
        // manage it).
        if (toRemove.Contains(AdministratorRole))
        {
            var adminRole = await roleManager.FindByNameAsync(AdministratorRole);
            var administratorCount = adminRole is null
                ? 0
                : await dbContext.UserRoles
                    .CountAsync(userRole => userRole.RoleId == adminRole.Id, cancellationToken);
            if (administratorCount <= 1)
            {
                throw new ApiException(
                    ErrorCodes.AdminCannotRemoveLastAdministrator, 409,
                    "You cannot remove the Administrator role from the last administrator.",
                    "لا يمكن إزالة دور المسؤول من آخر مسؤول.");
            }
        }

        if (toRemove.Count > 0)
        {
            await accounts.RemoveFromRolesAsync(user, toRemove, cancellationToken).EnsureSuccessAsync();
        }
        foreach (var roleName in toAdd)
        {
            await accounts.AddToRoleAsync(user, roleName, cancellationToken).EnsureSuccessAsync();
        }

        if (toRemove.Count > 0 || toAdd.Count > 0)
        {
            // Roll the security stamp so the user's live access tokens (which
            // carry the OLD roles + perms) are rejected, and the new grants
            // take effect on the next sign-in / refresh (SIMF-FDS-001 A.6).
            await accounts.UpdateSecurityStampAsync(user);
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.UserRolesUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = userId,
            SubjectEmail = user.Email,
            Detail = $"roles={string.Join(",", requested)}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} set roles on user {UserId}: {Roles}",
            actorUserId, userId, string.Join(",", requested));
    }
}
