namespace SIMF.Application.Abstractions;

/// <summary>The outcome of an upload malware scan.</summary>
public enum UploadScanVerdict
{
    /// <summary>No threat found.</summary>
    Clean,

    /// <summary>A known threat signature was found — the upload must be rejected.</summary>
    Infected,

    /// <summary>Scanning is disabled or unavailable; the upload was not scanned.</summary>
    Skipped,
}

/// <summary>The result of an <see cref="IUploadScanner"/> scan.</summary>
public sealed record UploadScanResult(UploadScanVerdict Verdict, string? ThreatName = null)
{
    public static readonly UploadScanResult Clean = new(UploadScanVerdict.Clean);
    public static readonly UploadScanResult Skipped = new(UploadScanVerdict.Skipped);
    public static UploadScanResult Infected(string threatName) =>
        new(UploadScanVerdict.Infected, threatName);

    public bool IsInfected => Verdict == UploadScanVerdict.Infected;
}

/// <summary>
/// A6-18 (NCA Secure Application-Development Standard) — scans files received from
/// untrusted sources for known malware before they are stored. The default
/// implementation detects the standard EICAR test signature and is the seam onto
/// which a production engine (ClamAV / Windows Defender / an ICAP gateway) is
/// registered via DI for full coverage.
/// </summary>
public interface IUploadScanner
{
    Task<UploadScanResult> ScanAsync(
        byte[] content, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>Helper so every upload path enforces the scan the same way.</summary>
public static class UploadScannerExtensions
{
    /// <summary>Scans the content and throws an <see cref="SIMF.Common.ApiException"/>
    /// (400 UPLOAD_MALWARE_DETECTED) when a threat is found.</summary>
    public static async Task EnsureCleanAsync(
        this IUploadScanner scanner, byte[] content, string fileName,
        CancellationToken cancellationToken = default)
    {
        var result = await scanner.ScanAsync(content, fileName, cancellationToken);
        if (result.IsInfected)
        {
            throw new SIMF.Common.ApiException(
                SIMF.Common.ErrorCodes.UploadMalwareDetected, 400,
                "The file failed a malware scan and was rejected.",
                "فشل فحص الملف بحثًا عن البرمجيات الخبيثة وتم رفضه.");
        }
    }

    /// <summary>D-568 / D-494 — the fail-closed variant used by the centralized
    /// file pipeline. Rejects an infected file (400) and, when
    /// <paramref name="failClosed"/> is true, also rejects a
    /// <see cref="UploadScanVerdict.Skipped"/> / scanner-unavailable verdict (409)
    /// rather than storing the file unscanned.</summary>
    public static async Task EnsureCleanFailClosedAsync(
        this IUploadScanner scanner, byte[] content, string fileName, bool failClosed,
        CancellationToken cancellationToken = default)
    {
        var result = await scanner.ScanAsync(content, fileName, cancellationToken);
        if (result.IsInfected)
        {
            throw new SIMF.Common.ApiException(
                SIMF.Common.ErrorCodes.UploadMalwareDetected, 400,
                "The file failed a malware scan and was rejected.",
                "فشل فحص الملف بحثًا عن البرمجيات الخبيثة وتم رفضه.");
        }
        if (failClosed && result.Verdict == UploadScanVerdict.Skipped)
        {
            throw new SIMF.Common.ApiException(
                SIMF.Common.ErrorCodes.UploadScanUnavailable, 409,
                "The file could not be scanned for malware right now; please try again shortly.",
                "تعذّر فحص الملف بحثًا عن البرمجيات الخبيثة حاليًا؛ يرجى المحاولة بعد قليل.");
        }
    }
}
