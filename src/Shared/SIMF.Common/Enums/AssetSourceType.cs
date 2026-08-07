namespace SIMF.Common.Enums;

/// <summary>How a media <c>Asset</c>'s bytes are sourced: an uploaded
/// file held out-of-row on disk (<see cref="Upload"/>), or an external URL
/// referenced in place (<see cref="ExternalLink"/>). Persisted as an int;
/// append-only.</summary>
public enum AssetSourceType
{
    Upload = 0,
    ExternalLink = 1,
}
