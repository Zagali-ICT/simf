// Covers: src/Edge/SIMF.MobileEdge/appsettings.json  (the published path set)
//         src/Edge/SIMF.MobileEdge/Program.cs        (the two boot guards)
//
// The edge is the host that lets the API stop being published. Everything that
// makes that safe is configuration, so configuration is what these assert.
//
// Text and JSON assertions over the checked-in files rather than a running host:
// owner-rule section 1.7 forbids csproj edits, so the edge gets no test project
// of its own and no new package. That buys less than an integration test would,
// and it buys the thing that actually matters - nobody can widen the published
// path set, or delete a boot guard, without a test going red.
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace SIMF.Api.Tests.Operations;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class MobileEdgeRoutingTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private static string EdgeFile(string name) =>
        File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Edge", "SIMF.MobileEdge", name));

    private static JsonElement Routes()
    {
        using var document = JsonDocument.Parse(EdgeFile("appsettings.json"));
        return document.RootElement
            .GetProperty("ReverseProxy").GetProperty("Routes").Clone();
    }

    /// <summary>The load-bearing security property of the whole tier separation.
    ///
    /// <para>The edge is published on the internet and the API is not, so the
    /// edge's route table is the entire list of things the internet can reach.
    /// Every published path must sit under the mobile surface. A route matching
    /// /api/v1/{**catch-all}, or anything reaching /admin, would republish the
    /// administrative API through the one host that is still exposed and undo the
    /// separation without any other test noticing.</para></summary>
    [Fact]
    public void The_edge_publishes_the_mobile_surface_and_nothing_else()
    {
        var paths = Routes().EnumerateObject()
            .Select(route => route.Value.GetProperty("Match").GetProperty("Path").GetString())
            .ToList();

        Assert.NotEmpty(paths);

        foreach (var path in paths)
        {
            Assert.NotNull(path);
            Assert.True(
                path!.StartsWith("/api/v1/app/", StringComparison.Ordinal),
                $"The mobile edge publishes '{path}', which is outside the mobile "
                + "surface. It is the only internet-facing host once the API is "
                + "unpublished, so every route it carries must sit under "
                + "/api/v1/app/. Verified against the Flutter app: every endpoint it "
                + "calls is under /app.");
        }
    }

    /// <summary>The destination must never be committed. The value is the API's
    /// private address, known only to the site, and the plausible wrong value is
    /// the public name - which sends the edge through the load balancer back to
    /// itself.</summary>
    [Fact]
    public void The_edge_ships_without_a_forwarding_destination()
    {
        using var document = JsonDocument.Parse(EdgeFile("appsettings.json"));
        var address = document.RootElement
            .GetProperty("ReverseProxy").GetProperty("Clusters").GetProperty("api")
            .GetProperty("Destinations").GetProperty("primary")
            .GetProperty("Address").GetString();

        Assert.Equal(string.Empty, address);
    }

    /// <summary>Both Production boot guards must stay. Each covers a failure that
    /// is silent rather than loud: no destination 502s every app user, and an
    /// unverified X-Forwarded-For lets any caller spoof its source address past
    /// the API's rate limiter and into the audit log.</summary>
    [Fact]
    public void The_edge_refuses_to_start_misconfigured_outside_development()
    {
        var program = EdgeFile("Program.cs");

        Assert.Contains("IsDevelopment()", program, StringComparison.Ordinal);
        Assert.Contains("ReverseProxy:KnownProxies", program, StringComparison.Ordinal);
        Assert.Contains(
            "ReverseProxy:Clusters:api:Destinations:primary:Address",
            program,
            StringComparison.Ordinal);

        // Two throws: one per guard.
        var throws = program.Split("throw new InvalidOperationException").Length - 1;
        Assert.True(
            throws >= 2,
            $"The edge declares {throws} boot guard(s); both the destination and the "
            + "proxy allowlist must refuse to start when unset.");
    }

    /// <summary>The forwarded-headers defaults trust loopback, which is not the
    /// proxy in front of this host. Left in place, anything on the box could forge
    /// a client address.</summary>
    [Fact]
    public void The_edge_clears_the_default_trusted_proxies_before_adding_its_own()
    {
        var program = EdgeFile("Program.cs");

        Assert.Contains("KnownProxies.Clear()", program, StringComparison.Ordinal);
        Assert.Contains("KnownIPNetworks.Clear()", program, StringComparison.Ordinal);
    }

    /// <summary>The edge's health endpoint must not probe the API. A check that
    /// fails because a downstream is unhealthy takes the edge out of rotation for
    /// a fault it cannot fix, turning one outage into two.</summary>
    [Fact]
    public void The_edge_health_endpoint_reports_on_itself_only()
    {
        var program = EdgeFile("Program.cs");

        Assert.Contains("MapGet(\"/health\"", program, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", program, StringComparison.Ordinal);
    }
}
