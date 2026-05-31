using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;

namespace SIMF.Application.IdentityAccess;

/// <inheritdoc />
public sealed class PermissionResolver(IPermissionRepository permissions) : IPermissionResolver
{
    public async Task<IReadOnlyList<string>> ResolveForRolesAsync(
        IEnumerable<string> roleNames, CancellationToken cancellationToken = default)
    {
        // An Administrator holds every permission via the wildcard, so we never
        // expand its grants into individual codes — that keeps the token small
        // and the super-admin un-lockout-able (PermissionCatalog).
        if (roleNames.Contains(AppRoles.Administrator))
        {
            return [PermissionCatalog.Wildcard];
        }

        return await permissions.GetCodesForRolesAsync(roleNames, cancellationToken);
    }
}
