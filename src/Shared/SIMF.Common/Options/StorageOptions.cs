namespace SIMF.Common.Options;

/// <summary>
/// Filesystem-storage settings, bound from the <c>Storage</c> configuration
/// section. Replaces four scattered <c>IConfiguration["Storage:..."]</c>
/// reads (R1 — D-074) so a key rename is a single change. The values are
/// supplied through the environment / <c>set-env</c> scripts and the
/// encryption key is never committed.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    // AvatarBase, VipPhotoBase and UserIdDocumentBase were removed on
    // 2026-08-05. D-568 moved all three stores into the unified StoredFile
    // store under FileStorage:RootPath - avatars, ID documents and VIP photos
    // are FileService values now, and the bespoke filesystem stores that read
    // these paths no longer exist. Nothing had read them since that cutover
    // except a boot gate that refused to start the API unless AvatarBase named
    // a directory the code would never open. An old deployment may still set
    // the environment variables; they bind to nothing and are ignored.

    /// <summary>Base64-encoded 32-byte AES-256-GCM key for the user-ID document
    /// encryption. Never committed; supplied via environment variable.</summary>
    public string UserIdDocumentEncryptionKey { get; set; } = string.Empty;

    /// <summary>Root directory the per-project log files live in (P6).
    /// Optional — falls back to <c>"logs"</c> when unset.</summary>
    public string LogDirectory { get; set; } = "logs";
}
