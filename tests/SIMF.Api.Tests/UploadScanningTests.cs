// NCA A6-18 — proves uploads are malware-scanned: the default scanner detects the
// EICAR test signature, and an upload carrying it is rejected before storage.
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Abstractions;
using SIMF.Application.Assets.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class UploadScanningTests : IClassFixture<SimfApiFactory>
{
    // The standard EICAR anti-malware test string (not a real virus).
    private static readonly byte[] Eicar = Encoding.ASCII.GetBytes(
        @"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly SimfApiFactory _factory;

    public UploadScanningTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Scanner_flags_eicar_and_passes_clean_content()
    {
        using var scope = _factory.Services.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<IUploadScanner>();

        var infected = await scanner.ScanAsync(Eicar, "evil.txt");
        Assert.True(infected.IsInfected);
        Assert.Equal("EICAR-Test-File", infected.ThreatName);

        var clean = await scanner.ScanAsync(PngHeader, "ok.png");
        Assert.Equal(UploadScanVerdict.Clean, clean.Verdict);
    }

    [Fact]
    public async Task Asset_upload_carrying_eicar_is_rejected_before_storage()
    {
        using var scope = _factory.Services.CreateScope();
        var assets = scope.ServiceProvider.GetRequiredService<IAssetService>();

        // Valid PNG magic bytes (passes the magic-byte gate) with EICAR embedded
        // after them, so the malware scan — not the type check — is what rejects it.
        var payload = PngHeader.Concat(Eicar).ToArray();

        var ex = await Assert.ThrowsAsync<ApiException>(() => assets.SetUploadAsync(
            Guid.NewGuid(), AssetCategory.SpeakerPhoto, Guid.NewGuid(), AssetKind.Image,
            payload, "image/png", "photo.png"));
        Assert.Equal(ErrorCodes.UploadMalwareDetected, ex.Code);
    }
}
