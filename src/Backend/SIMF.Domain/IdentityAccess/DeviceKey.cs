namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A registered device key for biometric (Face ID / Touch ID) sign-in. The
/// client generates an asymmetric keypair on first registration, keeps the
/// private half in the device's secure storage (Android
/// EncryptedSharedPreferences / iOS Keychain) and sends the public half here.
/// Enrolment needs both an emailed step-up code and an OS device-credential
/// confirmation. On later sign-ins the client signs a server-issued challenge
/// behind a biometric prompt, and the server verifies it against this row.
///
/// <para><b>The private key is software-bound, not hardware- or
/// biometric-bound.</b> The biometric prompt gates the code path that reaches
/// the key, not the key material itself. Binding it inside Android
/// Keystore/StrongBox (setUserAuthenticationRequired) or the iOS Secure Enclave
/// is planned hardening, and would not change the server contract, which is a
/// SubjectPublicKeyInfo in and an ES256 verify.</para>
///
/// <para><b>Algorithm:</b> ECDSA on the NIST P-256 curve, which is ES256 and so
/// JWS compatible. Chosen over Ed25519 because .NET verifies ECDSA from a
/// SubjectPublicKeyInfo with no NuGet dependency, and ES256 is what the iOS
/// Secure Enclave and Android StrongBox sign with natively.</para>
///
/// <para>The challenge is kept inline on this row rather than in a table of its
/// own: at most one is alive per device key, so a row always exists to hang it
/// on.</para>
/// </summary>
public sealed class DeviceKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>A real foreign key to <c>SimfUser.Id</c>: the user lives
    /// in the same Identity database. Deleting the user cascades to their
    /// keys.</summary>
    public Guid UserId { get; set; }

    /// <summary>Base64 of the SubjectPublicKeyInfo (DER) blob, so the server can
    /// import it directly with <c>ECDsa.ImportSubjectPublicKeyInfo</c>.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Only "ES256" is accepted; registration rejects anything else.
    /// The column exists so adding Ed25519 or ML-DSA later does not break the
    /// wire.</summary>
    public string Algorithm { get; set; } = "ES256";

    /// <summary>User-supplied device name such as "iPhone 15 Pro", so the owner
    /// can tell one row from another in the revoke list. Capped at 64
    /// characters; the contents are never interpreted.</summary>
    public string Label { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>Stamped by the same atomic update that consumes a verified
    /// challenge, so it records the last accepted signature rather than the last
    /// completed sign-in: a key whose owner turns out to be disabled still
    /// stamps it. Null until the key is first used.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Null while the key is active. Revoking is one-way and idempotent
    /// (admin or self-service); a revoked key can neither take a challenge nor
    /// sign in, and both are answered 401.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>The base64 nonce the client must sign, set when a challenge is
    /// issued. At most one is in flight per key: issuing overwrites it, and a
    /// successful verify clears it through a conditional update that matches
    /// only the row still holding this exact value, which is what makes a
    /// replayed signature fail. Revoking clears it too.</summary>
    public string? CurrentChallenge { get; set; }

    /// <summary>Five minutes after the challenge was issued. Past that the
    /// challenge counts as missing rather than as wrong.</summary>
    public DateTime? ChallengeExpiresAt { get; set; }
}
