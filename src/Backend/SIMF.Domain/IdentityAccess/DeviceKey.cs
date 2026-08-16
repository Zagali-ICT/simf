namespace SIMF.Domain.IdentityAccess;

/// <summary>A registered device key for biometric sign-in: the client keeps the
/// private half in device secure storage and signs a server-issued challenge.
/// <para><b>The private key is software-bound, not hardware- or biometric-bound</b>
/// — the prompt gates the code path reaching the key, not the key material;
/// Keystore/StrongBox/Secure-Enclave binding is planned hardening.</para>
/// <para>ECDSA P-256 (ES256) over Ed25519: .NET verifies it from a
/// SubjectPublicKeyInfo with no extra dependency, and mobile secure hardware
/// signs it natively.</para></summary>
public sealed class DeviceKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>Base64 of the SubjectPublicKeyInfo (DER) blob.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Only "ES256" is accepted; registration rejects anything else.</summary>
    public string Algorithm { get; set; } = "ES256";

    public string Label { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>The last accepted signature, not the last completed sign-in: a key
    /// whose owner turns out to be disabled still stamps it.</summary>
    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>The base64 nonce awaiting signature; at most one in flight per key.
    /// A verify clears it through a conditional update matching this exact value,
    /// which is what makes a replayed signature fail.</summary>
    public string? CurrentChallenge { get; set; }

    public DateTime? ChallengeExpiresAt { get; set; }
}
