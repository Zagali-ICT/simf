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

    /// <summary>Encrypts free text that the USER controls, with no idempotence
    /// shortcut.
    ///
    /// <para><see cref="Encrypt"/> returns a value that already begins
    /// <c>enc:1:</c> unchanged, which is right for a re-saved identifier and
    /// wrong for anything a person can type: a visitor who opens a chat message
    /// with the literal marker would have it stored in the CLEAR, and read back
    /// through a decryptor that then tries to base64-decode the rest of their
    /// sentence. Use this for any column whose plaintext is free text.</para></summary>
    string? EncryptFreeText(string? plaintext);

    /// <summary>Decrypts an <c>enc:1:</c> value. Null/empty and legacy plaintext
    /// (no marker) are returned unchanged. Throws if a marked value is supplied
    /// but no key is configured.</summary>
    string? Decrypt(string? stored);

    /// <summary>H-1 — a deterministic <b>blind index</b> of a value: a keyed
    /// HMAC-SHA256 hex digest (64 chars). Because <see cref="Encrypt"/> uses a
    /// random nonce, the encrypted PII columns can never be equality-queried or
    /// unique-indexed; this stable keyed digest can, so the duplicate-identity
    /// guard and its filtered UNIQUE indexes key off the digest instead of the
    /// ciphertext. Null/empty is returned as null. Throws if no key is
    /// configured.</summary>
    string? BlindIndex(string? value);
}
