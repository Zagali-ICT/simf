using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>Persistence for <see cref="Permission"/> records.</summary>
public interface IPermissionRepository
{
    /// <summary>All permissions in the catalogue.</summary>
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds a permission by its code; null if there is no match.</summary>
    Task<Permission?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>The distinct permission codes granted to any of the named
    /// roles — the set baked into a user's <c>perm</c> claim at token-mint
    /// time. Returns an empty list when no role name matches.</summary>
    Task<IReadOnlyList<string>> GetCodesForRolesAsync(
        IEnumerable<string> roleNames, CancellationToken cancellationToken = default);

    /// <summary>Adds a new permission.</summary>
    Task AddAsync(Permission permission, CancellationToken cancellationToken = default);
}
