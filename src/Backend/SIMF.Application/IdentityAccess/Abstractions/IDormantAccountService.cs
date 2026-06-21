namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// A1-19 (NCA) — disables accounts that have been inactive beyond the configured
/// threshold (last successful sign-in, or creation time if never signed in).
/// </summary>
public interface IDormantAccountService
{
    /// <summary>Disables every Approved account inactive beyond
    /// <c>IdentityLifecycle:DormantAccountDisableDays</c> (no-op when 0). Rolls each
    /// account's security stamp and revokes its sessions. Returns the count disabled.</summary>
    Task<int> DisableDormantAccountsAsync(CancellationToken cancellationToken = default);
}
