using SIMF.Application.Abstractions;
using SIMF.Infrastructure.Files;
using Xunit;

namespace SIMF.Api.Tests.Files;

/// <summary>D-568 — unit cover for the clamd INSTREAM reply parser, in isolation
/// from a live daemon. <c>OK</c> = clean, <c>… FOUND</c> = infected (with the
/// signature name), anything else = Skipped so the fail-closed layer decides.</summary>
[Trait(TestAreas.TraitName, TestAreas.Files)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class ClamAvResponseParsingTests
{
    [Theory]
    [InlineData("stream: OK")]
    [InlineData("OK")]
    public void An_ok_reply_is_clean(string reply)
    {
        var result = ClamAvUploadScanner.ParseResponse(reply, "x.png");

        Assert.Equal(UploadScanVerdict.Clean, result.Verdict);
    }

    [Fact]
    public void A_found_reply_is_infected_and_carries_the_signature_name()
    {
        var result = ClamAvUploadScanner.ParseResponse("stream: Eicar-Test-Signature FOUND", "x.txt");

        Assert.Equal(UploadScanVerdict.Infected, result.Verdict);
        Assert.Equal("Eicar-Test-Signature", result.ThreatName);
        Assert.True(result.IsInfected);
    }

    [Fact]
    public void A_found_reply_with_a_dotted_signature_name_parses()
    {
        var result = ClamAvUploadScanner.ParseResponse("stream: Win.Test.EICAR_HDB-1 FOUND", "x.exe");

        Assert.Equal(UploadScanVerdict.Infected, result.Verdict);
        Assert.Equal("Win.Test.EICAR_HDB-1", result.ThreatName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("INSTREAM size limit exceeded")]
    [InlineData("ERROR")]
    [InlineData("stream: Access denied. ERROR")]
    public void An_error_or_unknown_reply_is_skipped(string reply)
    {
        var result = ClamAvUploadScanner.ParseResponse(reply, "x.bin");

        Assert.Equal(UploadScanVerdict.Skipped, result.Verdict);
    }
}
