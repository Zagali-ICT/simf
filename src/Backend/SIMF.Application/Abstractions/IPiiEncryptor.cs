namespace SIMF.Application.Abstractions;

/// <summary>
/// A2-10 (NCA Secure Application-Development Standard) — encrypts the most
/// sensitive PII identifier columns at rest. Applied transparently by an EF Core
/// value converter on <c>UserProfile</c> (national ID / Iqama / passport / mobile).
///
/// <para>The stored form is <c>enc:1:&lt;base64(nonce|tag|ciphertext)&gt;</c> using
/// AES-256-GCM. Values that do not carry the <c>enc:1:</c> marker are treated as
/// legacy plaintext on read and returned unchanged, so the converter is
/// non-breaking for rows written before encryption was switched on; any update to
/// such a row re-writes it encrypted.</para>
/// </summary>
public interface IPiiEncryptor
{
    /// <summary>True when a valid encryption key is configured. The boot guard
    /// fails fast in Production when this is false; design-time tooling tolerates
    /// a missing key (it never encrypts/decrypts).</summary>
    bool IsKeyConfigured { get; }

    /// <summary>Encrypts a plaintext value to the <c>enc:1:</c> form. Null/empty is
    /// returned unchanged. Throws if no key is configured.</summary>
    string? Encrypt(string? plaintext);

    /// <summary>Decrypts an <c>enc:1:</c> value. Null/empty and legacy plaintext
    /// (no marker) are returned unchanged. Throws if a marked value is supplied
    /// but no key is configured.</summary>
    string? Decrypt(string? stored);
}
