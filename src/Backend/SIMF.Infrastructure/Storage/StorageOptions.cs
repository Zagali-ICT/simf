namespace SIMF.Infrastructure.Storage;

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

    /// <summary>Root directory the filesystem avatar storage writes into.</summary>
    public string AvatarBase { get; set; } = string.Empty;

    /// <summary>Root directory the encrypted user-ID document storage writes into.</summary>
    public string UserIdDocumentBase { get; set; } = string.Empty;

    /// <summary>Base64-encoded 32-byte AES-256-GCM key for the user-ID document
    /// encryption. Never committed; supplied via environment variable.</summary>
    public string UserIdDocumentEncryptionKey { get; set; } = string.Empty;

    /// <summary>Root directory the per-project log files live in (P6).
    /// Optional — falls back to <c>"logs"</c> when unset.</summary>
    public string LogDirectory { get; set; } = "logs";
}
