namespace SIMF.Common.Options;

/// <summary>D-568 — settings for the centralized file store, bound from the
/// <c>FileStorage</c> configuration section. One root directory for every file;
/// the <see cref="EncryptionKey"/> is the Key-Encryption-Key (KEK) that wraps
/// each file's per-file Data-Encryption-Key (envelope encryption). The key is
/// never committed — it is supplied through the environment / <c>set-env</c>
/// scripts, the same way the JWT signing key and the ID-document key are.</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Root directory every stored file's bytes live under (one
    /// per-<c>Service</c> sub-folder beneath it).
    ///
    /// <para>Empty means "use the machine's shared application-data directory"
    /// — see <c>FilesystemFileStorageProvider.DefaultRoot</c>. It deliberately
    /// does NOT default to a relative path: a relative root resolves against
    /// the process working directory, which during development and testing is
    /// the project folder inside the repository, so uploaded avatars and
    /// ID-document scans landed in the source tree and were committed by
    /// accident. An absolute machine-level default cannot do that.</para></summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Base64-encoded 32-byte AES-256 active KEK. Required before any
    /// encrypted file is read or written; the cipher refuses to construct without
    /// a valid value (boot-fail-fast).</summary>
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>Version stamp of the active KEK, written into each encrypted
    /// blob's header so a rotation can re-wrap the per-file keys without
    /// re-encrypting the bodies.</summary>
    public byte KekVersion { get; set; } = 1;

    /// <summary>Optional previous KEK (base64 32-byte), kept available during a
    /// rotation window so blobs wrapped under <see cref="PreviousKekVersion"/>
    /// still decrypt until they are re-wrapped. Empty when no rotation is in flight.</summary>
    public string PreviousEncryptionKey { get; set; } = string.Empty;

    /// <summary>Version stamp of <see cref="PreviousEncryptionKey"/>.</summary>
    public byte PreviousKekVersion { get; set; }
}
