namespace SIMF.Application.Files.Abstractions;

/// <summary>Envelope encryption for stored-file bytes. Each file is
/// sealed under its own random Data-Encryption-Key (DEK), which is itself wrapped
/// by a versioned Key-Encryption-Key (KEK). The wrapped DEK travels in the blob
/// header, so destroying one file's header crypto-shreds that file alone, and a
/// KEK rotation only re-wraps the small DEKs (never the bodies).</summary>
public interface IFileCipher
{
    /// <summary>The on-disk blob format version this cipher writes — stored on the
    /// <c>StoredFile</c> row so the read path can select the right decryptor.</summary>
    byte CurrentFormatVersion { get; }

    /// <summary>The KEK version <see cref="Encrypt"/> wraps under right now —
    /// stamped into the blob header AND recorded on the <c>StoredFile</c> row, so
    /// rotation progress can be counted in SQL instead of by reading the second
    /// byte of every blob on disk. Read it from the cipher rather than from
    /// configuration: the recorded version must be the one that actually wrapped
    /// the key, not a setting that could be re-read after a reload.</summary>
    byte ActiveKekVersion { get; }

    /// <summary>Seals <paramref name="plaintext"/> into a self-describing blob
    /// (format version + KEK version + wrapped DEK + nonce + tag + ciphertext).</summary>
    byte[] Encrypt(byte[] plaintext);

    /// <summary>Opens a blob produced by <see cref="Encrypt"/>. Throws
    /// <see cref="System.Security.Cryptography.CryptographicException"/> on a tag
    /// mismatch / wrong key and <see cref="System.NotSupportedException"/> on an
    /// unknown format or KEK version.</summary>
    byte[] Decrypt(byte[] blob);
}
