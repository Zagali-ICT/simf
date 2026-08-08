namespace SIMF.ApiClient.Tests;

/// <summary>Tests for the shared API base-address resolver used by the host
/// projects' typed-client registrations (SIMF.Web and SIMF.ControlPanel).</summary>
public sealed class SimfApiBaseAddressTests
{
    [Fact]
    public void Resolve_returns_the_configured_uri()
    {
        var uri = SimfApiBaseAddress.Resolve("https://api.simf.test/", isDevelopment: false);

        Assert.Equal(new Uri("https://api.simf.test/"), uri);
    }

    [Fact]
    public void Resolve_allows_http_in_development()
    {
        var uri = SimfApiBaseAddress.Resolve("http://localhost:5175/", isDevelopment: true);

        Assert.Equal(new Uri("http://localhost:5175/"), uri);
    }

    [Fact]
    public void Resolve_rejects_http_outside_development()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => SimfApiBaseAddress.Resolve("http://localhost:5175/", isDevelopment: false));

        Assert.Contains("HTTPS", ex.Message);
    }

    [Fact]
    public void Resolve_rejects_a_missing_value()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => SimfApiBaseAddress.Resolve(null, isDevelopment: true));

        Assert.Contains("Api:BaseUrl", ex.Message);
    }
}
