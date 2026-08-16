using System.Net;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration test for the readiness endpoint (SIMF-OPS-001 section 13,
/// Amendment A.4).
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class HealthEndpointTests(SimfApiFactory factory)
    : IClassFixture<SimfApiFactory>
{
    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
