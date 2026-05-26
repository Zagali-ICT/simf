using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Mints the opaque visitor QR-ID used at event entry (decision D-046).
/// One QR per <see cref="UserProfile"/>, generated the moment the
/// owning account transitions to <c>AccountState.Approved</c>;
/// idempotent (a second mint on an already-set profile is a no-op).
///
/// <para>D-106: takes a <see cref="UserProfile"/> rather than a
/// <see cref="SimfUser"/> because the QR lives on the profile row now
/// (it is profile-scope — Admin-typed accounts neither attend nor get
/// a QR). The caller ensures a UserProfile row exists for the user
/// before minting.</para>
/// </summary>
public interface IQrIdMinter
{
    /// <summary>
    /// Mints a new QR id on <paramref name="profile"/> if one isn't set
    /// yet. Returns the resulting QR id (existing or new). Does not
    /// persist the profile — the caller commits in the surrounding unit
    /// of work.
    /// </summary>
    Task<string> MintIfMissingAsync(UserProfile profile, CancellationToken cancellationToken = default);
}
