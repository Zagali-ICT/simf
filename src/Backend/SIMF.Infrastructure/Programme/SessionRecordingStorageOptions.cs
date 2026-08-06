namespace SIMF.Infrastructure.Programme;

/// <summary>Upload-size options for session recordings.
/// The recording bytes moved to the unified <c>StoredFile</c>
/// store, so this class is kept only for <see cref="MaxUploadBytes"/> — the
/// per-request body/multipart ceiling the recording upload endpoint raises.
/// The old <c>RootPath</c> is gone with the bespoke recording store; an unknown
/// key in an old configuration file binds to nothing and is ignored.</summary>
public sealed class SessionRecordingStorageOptions
{
    public const string SectionName = "SessionRecordingStorage";

    /// <summary>Max accepted upload size, in bytes (default 1 GiB). The upload
    /// endpoint raises the request-body + multipart limits to this value for
    /// that one request and rejects anything larger — so the global DoS
    /// posture on every other endpoint is unchanged.</summary>
    public long MaxUploadBytes { get; set; } = 1_073_741_824;
}
