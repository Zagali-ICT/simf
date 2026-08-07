namespace SIMF.ApiClient.Tests;

/// <summary>Tests for the shared API base-address resolver used by the host
/// projects' typed-client registrations (SIMF.Web and SIMF.ControlPanel).</summary>
public sealed class SimfApiBaseAddressTests
{
    [Fact]
    public void Resolve_returns_the_configured_uri()
    {
        var uri = SimfApiBaseAddress.Resolve(
            "https://api.simf.test/", isDevelopment: false, allowSelfSignedCertificate: false);

        Assert.Equal(new Uri("https://api.simf.test/"), uri);
    }

    [Fact]
    public void Resolve_allows_http_in_development()
    {
        var uri = SimfApiBaseAddress.Resolve(
            "http://localhost:5175/", isDevelopment: true, allowSelfSignedCertificate: false);

        Assert.Equal(new Uri("http://localhost:5175/"), uri);
    }

    [Fact]
    public void Resolve_rejects_http_outside_development()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => SimfApiBaseAddress.Resolve(
                "http://localhost:5175/", isDevelopment: false, allowSelfSignedCertificate: false));

        Assert.Contains("HTTPS", ex.Message);
    }

    [Fact]
    public void Resolve_rejects_a_missing_value()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => SimfApiBaseAddress.Resolve(
                null, isDevelopment: true, allowSelfSignedCertificate: false));

        Assert.Contains("Api:BaseUrl", ex.Message);
    }

    // --- The trust-all guard -------------------------------------------------
    // Why the pairing is refused rather than trusted: see the comment on the
    // guard in SimfApiBaseAddress.Resolve.

    [Fact]
    public void Resolve_rejects_the_trust_all_against_a_public_origin()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => SimfApiBaseAddress.Resolve(
                "https://api.simrsnf.com/", isDevelopment: false, allowSelfSignedCertificate: true));

        // Names the setting AND the offending address, so the operator can act
        // on the message without reading the source.
        Assert.Contains("Api:AllowSelfSignedCertificate", ex.Message);
        Assert.Contains("api.simrsnf.com", ex.Message);
    }

    [Theory]
    // The historical production pairing: a loopback hop cannot be intercepted,
    // and its certificate cannot match "localhost".
    [InlineData("https://localhost:12340/", false, true)]
    // Uri.IsLoopback covers the loopback IPs as well as the name - pinned in
    // both families so a future rewrite cannot narrow it to a string compare.
    [InlineData("https://127.0.0.1:12340/", false, true)]
    [InlineData("https://[::1]:12340/", false, true)]
    // The hardened pairing this repo ships after D-872.
    [InlineData("https://api.simrsnf.com/", false, false)]
    // A developer running against a local API over a self-signed cert on a
    // machine name is not the threat being guarded, and blocking it would only
    // teach people to turn the guard off.
    [InlineData("https://simf-dev.local/", true, true)]
    public void Resolve_accepts_every_safe_pairing(
        string baseUrl, bool isDevelopment, bool allowSelfSignedCertificate)
    {
        var uri = SimfApiBaseAddress.Resolve(baseUrl, isDevelopment, allowSelfSignedCertificate);

        Assert.Equal(new Uri(baseUrl), uri);
    }
}
