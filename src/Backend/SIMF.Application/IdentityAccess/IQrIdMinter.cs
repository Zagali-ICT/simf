using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Mints the opaque visitor QR-ID used at event entry (decision D-046).
/// One QR per user, generated the moment the account transitions to
/// <c>AccountState.Approved</c>; idempotent (a second mint on an already-
/// approved user is a no-op).
/// </summary>
public interface IQrIdMinter
{
    /// <summary>
    /// Mints a new QR id for <paramref name="user"/> if one isn't set yet.
    /// Returns the resulting QR id (existing or new). Does not persist
    /// the user — the caller commits in the surrounding unit of work.
    /// </summary>
    Task<string> MintIfMissingAsync(SimfUser user, CancellationToken cancellationToken = default);
}
