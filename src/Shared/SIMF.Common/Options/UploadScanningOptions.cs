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
}
