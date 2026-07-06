namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Resolves Identity-owned user attributes (currently email) for App-side
/// services that must reference a user across the physical DB boundary (D-157).
/// The implementation queries ONLY <c>SimfIdentityDbContext</c>; callers keep
/// their own <c>SIMF_App</c> query and merge the result in memory — never a
/// cross-database JOIN, never a unit of work spanning both contexts.
/// </summary>
public interface IIdentityUserDirectory
{
    /// <summary>The user's email, or <c>null</c> when the id is unknown.</summary>
    Task<string?> GetEmailAsync(Guid userId, CancellationToken cancellationToken = default);
}
