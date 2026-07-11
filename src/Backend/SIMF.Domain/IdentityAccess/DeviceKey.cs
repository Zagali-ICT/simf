namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// D-172 (gap doc G10, PDF §2.5) — a registered device key for biometric
/// (Face ID / Touch ID) sign-in. The client generates an asymmetric
/// keypair on first registration, stores the private half in the device's
/// secure storage (Android EncryptedSharedPreferences / iOS Keychain), and
/// sends the public half to the server. Enrolment is gated by a server
/// step-up code AND an OS device-credential confirmation (D-738). On
/// subsequent sign-ins the client signs a server-issued challenge
/// with the private key after a biometric prompt; the server verifies
/// against this stored public key.
///
/// <para><b>As-built note (D-738):</b> the private key is software-bound
/// (secure storage), not hardware/biometric-bound — the biometric prompt
/// gates the code path, not the key material. Binding the key inside Android
/// Keystore/StrongBox (setUserAuthenticationRequired) and the iOS Secure
/// Enclave is a planned Tier-2 hardening; the server contract (SPKI + ES256
/// verify) is unchanged by it.</para>
///
/// <para><b>Algorithm:</b> ECDSA on the NIST P-256 curve (ES256 / JWS
/// compatible). Chosen over Ed25519 because .NET 10 has first-class
/// support for ECDSA + SubjectPublicKeyInfo without any NuGet
/// dependency, and ES256 is the JWS standard for biometric flows on
/// both iOS Secure Enclave and Android StrongBox.</para>
///
/// <para><b>Challenge storage:</b> the per-device challenge is stored
/// inline on this row (<see cref="CurrentChallenge"/> +
/// <see cref="ChallengeExpiresAt"/>) rather than in a separate table —
/// at most one challenge is alive per device key at any time, so a
/// row already exists to attach it to. The challenge is consumed on
/// successful verify or after expiry (5 minutes).</para>
/// </summary>
public sealed class DeviceKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user this key binds to. Real FK to
    /// <see cref="SimfUser.Id"/> (same Identity DbContext).</summary>
    public Guid UserId { get; set; }

    /// <summary>The public key as a base64-encoded
    /// SubjectPublicKeyInfo (DER) blob. Importable on the server with
    /// <c>ECDsa.ImportSubjectPublicKeyInfo</c>.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Algorithm name — frozen as "ES256" for v1. Kept as a
    /// column so a future addition of Ed25519 / ML-DSA does not break
    /// the wire.</summary>
    public string Algorithm { get; set; } = "ES256";

    /// <summary>User-supplied device label — e.g. "iPhone 15 Pro" — so
    /// the user can identify the row in the revoke surface. Up to 64
    /// chars; the server does not validate the contents.</summary>
    public string Label { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the key was last used for a successful sign-in.
    /// Null until first sign-in completes.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>When the key was revoked (admin or self-service).
    /// Null while active. Sign-in with a revoked key returns 401.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>The current challenge nonce as a base64 string. Set
    /// when the client requests a challenge; cleared on consume.
    /// Single in-flight challenge per device key.</summary>
    public string? CurrentChallenge { get; set; }

    /// <summary>When <see cref="CurrentChallenge"/> expires. A
    /// challenge older than this is treated as missing.</summary>
    public DateTimeOffset? ChallengeExpiresAt { get; set; }
}
