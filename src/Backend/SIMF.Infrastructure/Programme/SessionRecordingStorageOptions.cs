namespace SIMF.Infrastructure.Programme;

/// <summary>P3.2b — D-232 (D-213): upload-size options for session recordings.
/// D-568 (Wave C S7): the recording bytes moved to the unified <c>StoredFile</c>
/// store, so this class is kept only for <see cref="MaxUploadBytes"/> — the
/// per-request body/multipart ceiling the recording upload endpoint raises.
/// <c>RootPath</c> is retained for config-binding compatibility but is no longer
/// read (the bespoke recording store is gone).</summary>
public sealed class SessionRecordingStorageOptions
{
    public const string SectionName = "SessionRecordingStorage";

    /// <summary>Vestigial (D-568 S7) — no longer read; kept so the existing
    /// <c>SessionRecordingStorage:RootPath</c> config key still binds cleanly.</summary>
    public string RootPath { get; set; } = "App_Data/recordings";

    /// <summary>Max accepted upload size, in bytes (default 1 GiB). The upload
    /// endpoint raises the request-body + multipart limits to this value for
    /// that one request and rejects anything larger — so the global DoS
    /// posture on every other endpoint is unchanged.</summary>
    public long MaxUploadBytes { get; set; } = 1_073_741_824;
}
