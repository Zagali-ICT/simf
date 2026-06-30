namespace SIMF.Common.Options;

/// <summary>
/// A6-18 (NCA) — upload malware-scanning settings, bound from the
/// <c>UploadScanning</c> configuration section.
/// </summary>
public sealed class UploadScanningOptions
{
    public const string SectionName = "UploadScanning";

    /// <summary>
    /// Whether uploaded files are scanned before storage. Default <c>true</c> —
    /// the built-in EICAR detector is cheap and never rejects a legitimate file,
    /// so it is safe to leave on. Set to <c>false</c> only to disable scanning
    /// entirely (e.g. when an external gateway already scans the traffic).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>D-568 — which scan engine backs <c>IUploadScanner</c>:
    /// <c>"Default"</c> (the built-in EICAR detector) or <c>"ClamAV"</c> (a real
    /// clamd daemon over TCP). Case-insensitive.</summary>
    public string Engine { get; set; } = "Default";

    /// <summary>D-568 / D-494 — when true, a <c>Skipped</c> / scanner-unavailable
    /// verdict on the centralized file pipeline is rejected (fail-closed) rather
    /// than stored unscanned. The centralized FileService applies this in the
    /// Production environment; dev/test (where scanning may be disabled) pass.</summary>
    public bool FailClosed { get; set; } = true;

    /// <summary>clamd host (when <see cref="Engine"/> is <c>ClamAV</c>).</summary>
    public string ClamAvHost { get; set; } = "localhost";

    /// <summary>clamd TCP port (default 3310).</summary>
    public int ClamAvPort { get; set; } = 3310;

    /// <summary>Per-scan timeout, seconds.</summary>
    public int ClamAvTimeoutSeconds { get; set; } = 30;

    /// <summary>INSTREAM chunk size, bytes (default 64 KiB).</summary>
    public int ClamAvChunkBytes { get; set; } = 65536;
}
