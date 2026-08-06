namespace SIMF.Common.Enums;

/// <summary>Whether a <c>StoredFile</c> holds uploaded bytes or just
/// references an external URL. Preserves the upload-vs-link capability the
/// unified store inherits from <see cref="AssetSourceType"/> (D-357): an
/// <see cref="ExternalLink"/> row stores no bytes and the download endpoint
/// 302-redirects to the URL.
///
/// <para>Persisted as an int; <b>append-only</b>.</para></summary>
public enum FileSourceType
{
    /// <summary>Bytes stored out-of-row on disk under <c>StorageKey</c>.</summary>
    Upload = 0,

    /// <summary>An external URL referenced in place (no bytes stored).</summary>
    ExternalLink = 1,
}
