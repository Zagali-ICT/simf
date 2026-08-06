namespace SIMF.Common.Enums;

/// <summary>The media kind a unified <c>Asset</c> carries. <see cref="Video"/>
/// assets are external links only (no large binaries are stored on disk).
/// Persisted as an int; append-only.</summary>
public enum AssetKind
{
    Image = 0,
    Video = 1,
    Document = 2,
}
