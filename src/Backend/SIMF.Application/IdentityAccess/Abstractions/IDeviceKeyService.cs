using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Biometric (Face ID / Touch ID) sign-in.
/// Owns the device-key registration ceremony, challenge issuance, and
/// signature verification + token mint.
/// </summary>
public interface IDeviceKeyService
{
    Task<DeviceKeyEntry> RegisterAsync(
        Guid callerUserId,
        RegisterDeviceKeyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Issue + email a one-time step-up code to the signed-in
    /// caller's own address, to be supplied on the following
    /// <see cref="RegisterAsync"/> call. Capped per window like the sign-in OTP;
    /// returns the masked recipient + lifetime (never the plaintext code).</summary>
    Task<SendBiometricStepUpResponse> IssueEnrolStepUpAsync(
        Guid callerUserId,
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

    /// <summary>
    /// Revokes every active device key the user holds, and returns how many were
    /// revoked so the caller can tell the owner what just happened. Idempotent:
    /// already-revoked keys are left alone and counted as zero.
    ///
    /// <para>This exists so a password change or reset can end the biometric
    /// credentials along with the refresh tokens. Without it, revoking every
    /// session still left a device key that mints a brand-new one through the
    /// anonymous sign-in endpoint, so the standard advice given to a compromised
    /// user did not actually evict the attacker.</para>
    /// </summary>
    Task<int> RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
