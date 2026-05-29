using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// D-172 (gap doc G10, PDF §2.5) — biometric (Face ID / Touch ID) sign-in.
/// Owns the device-key registration ceremony, challenge issuance, and
/// signature verification + token mint.
/// </summary>
public interface IDeviceKeyService
{
    Task<DeviceKeyEntry> RegisterAsync(
        Guid callerUserId,
        RegisterDeviceKeyRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceKeyEntry>> ListMineAsync(
        Guid callerUserId,
        CancellationToken cancellationToken = default);

    Task<DeviceKeyChallenge> IssueChallengeAsync(
        Guid deviceKeyId,
        CancellationToken cancellationToken = default);

    /// <summary>Verify the signature against the stored public key and
    /// return a JWT pair. Returns null when the verification fails so
    /// the endpoint can map to 401 without leaking which step failed.</summary>
    Task<AuthTokens?> SignInWithDeviceKeyAsync(
        SignInWithDeviceKeyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Revoke a device key. Self-service path passes the
    /// caller's own user id and the helper rejects keys not bound to
    /// them; the admin path passes <c>actorIsAdministrator=true</c> and
    /// the user-id check is skipped.</summary>
    Task RevokeAsync(
        Guid actorUserId,
        Guid deviceKeyId,
        bool actorIsAdministrator,
        CancellationToken cancellationToken = default);
}
