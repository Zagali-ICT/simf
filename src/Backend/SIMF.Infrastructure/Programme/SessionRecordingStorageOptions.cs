namespace SIMF.Infrastructure.Programme;

/// <summary>P3.2b — D-232 (D-213): filesystem storage options for session
/// recordings. Mirrors <see cref="SpeakerPresentationStorageOptions"/>
/// (own top-level section + relative <c>App_Data</c> root) rather than the
/// literal <c>Storage:SessionRecordingBase</c> example in D-213 — the two
/// are equivalent and matching the most recent file-seam convention keeps
/// the codebase consistent.</summary>
public sealed class SessionRecordingStorageOptions
{
    public const string SectionName = "SessionRecordingStorage";

    public string RootPath { get; set; } = "App_Data/recordings";

    /// <summary>Max accepted upload size, in bytes (default 1 GiB). The upload
    /// endpoint raises the request-body + multipart limits to this value for
    /// that one request and rejects anything larger — so the global DoS
    /// posture on every other endpoint is unchanged.</summary>
    public long MaxUploadBytes { get; set; } = 1_073_741_824;
}
