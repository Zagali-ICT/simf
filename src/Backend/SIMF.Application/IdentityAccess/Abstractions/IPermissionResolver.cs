namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Resolves the permission codes a user holds from their roles — the set baked
/// into the <c>perm</c> claim at token-mint time (SIMF-RPM-001 §8). An
/// Administrator resolves to the single wildcard code; every other role
/// resolves to its seeded / assigned grants.
/// </summary>
public interface IPermissionResolver
{
    Task<IReadOnlyList<string>> ResolveForRolesAsync(
        IEnumerable<string> roleNames, CancellationToken cancellationToken = default);
}
