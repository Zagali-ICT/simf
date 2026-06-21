using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Abstractions;
using SIMF.Common.Options;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// A6-18 (NCA) — the default upload scanner. Detects the standard EICAR
/// anti-malware test signature anywhere in the file, so the scanning seam is
/// enforced and verifiable out of the box without an external engine. For full
/// production coverage, register a real engine (ClamAV / Windows Defender / an
/// ICAP gateway) as <see cref="IUploadScanner"/> in DI — every upload path already
/// calls the seam, so it is a one-line swap.
/// </summary>
internal sealed class DefaultUploadScanner(
    IOptions<UploadScanningOptions> options,
    ILogger<DefaultUploadScanner> logger) : IUploadScanner
{
    // The 68-byte EICAR standard anti-malware test string (not a real virus).
    private static readonly byte[] EicarSignature = Encoding.ASCII.GetBytes(
        @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

    public Task<UploadScanResult> ScanAsync(
        byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return Task.FromResult(UploadScanResult.Skipped);
        }
        if (Contains(content, EicarSignature))
        {
            logger.LogWarning(
                "Upload '{File}' rejected — EICAR test signature detected.", fileName);
            return Task.FromResult(UploadScanResult.Infected("EICAR-Test-File"));
        }
        return Task.FromResult(UploadScanResult.Clean);
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }
        var last = haystack.Length - needle.Length;
        for (var i = 0; i <= last; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                return true;
            }
        }
        return false;
    }
}
