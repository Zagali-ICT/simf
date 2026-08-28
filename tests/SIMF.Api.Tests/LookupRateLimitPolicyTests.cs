using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common.Options;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// A type-ahead must not share the credential limiter.
///
/// <para><b>The defect this pins.</b> <c>GET /app/organisations</c> is the
/// organisation search behind the sign-up profile step: it fires once per typing
/// pause. It carried <c>RequireRateLimiting("auth")</c> — and <c>auth</c> is the
/// CREDENTIAL policy, 20 requests a minute per IP, shared with sign-in, the
/// email-OTP step and password reset. So typing an employer name spent the
/// visitor's own sign-in budget, and a long enough name locked them out of
/// logging in. Reported from the device, 2026-08-28.</para>
///
/// <para><b>Why the obvious fix was refused.</b> Raising <c>auth</c>'s limit to
/// fit a type-ahead would have lifted the brute-force cap off every credential
/// path at once — the policy is on 120 endpoint files. The lookup moves off it
/// instead, onto a policy of its own that is generous but still bounded, because
/// an unauthenticated-cost search is a cheap amplification target.</para>
///
/// <para>Asserted in BOTH directions. A route LOSING the lookup policy is the
/// original defect returning; a route GAINING it is a decision to take a caller
/// off the credential cap, which should be argued for rather than merged
/// quietly.</para>
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class LookupRateLimitPolicyTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public LookupRateLimitPolicyTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    /// <summary>The reviewed set: reference-data reads a UI queries repeatedly.</summary>
    private static readonly Dictionary<string, string> ExpectedLookupRoutes = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["/api/v1/app/organisations"] =
            "The organisation type-ahead on the sign-up profile step — one "
            + "request per typing pause, behind a 350 ms client debounce.",
    };

    [Fact]
    public void The_lookup_policy_is_on_exactly_the_reviewed_routes()
    {
        var lookup = RoutesWithPolicy(RateLimitOptions.LookupPolicy);

        // A sweep that matches nothing passes silently.
        Assert.True(
            lookup.Count > 0,
            "No endpoint declared the lookup policy at all — the enumeration is "
            + "broken, not the policy.");

        var missing = ExpectedLookupRoutes.Keys
            .Where(path => !lookup.Contains(path, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var unexpected = lookup
            .Where(path => !ExpectedLookupRoutes.ContainsKey(path))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Reference-data lookup(s) LOST the lookup policy. If they fell back "
            + "to \"auth\", typing in that field spends the user's sign-in "
            + "budget and can lock them out of logging in:\n  "
            + string.Join("\n  ", missing));
        Assert.True(
            unexpected.Count == 0,
            "Endpoint(s) newly moved OFF the credential cap onto the lookup "
            + "policy. Justify and add to ExpectedLookupRoutes:\n  "
            + string.Join("\n  ", unexpected));
    }

    [Fact]
    public void The_organisation_search_is_not_on_the_credential_policy()
    {
        // Stated separately from the set assertion above so the failure message
        // names the actual consequence rather than a diff.
        var auth = RoutesWithPolicy("auth");

        Assert.DoesNotContain(
            "/api/v1/app/organisations",
            auth,
            StringComparer.OrdinalIgnoreCase);
    }

    private List<string> RoutesWithPolicy(string policyName) =>
        _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
                == policyName)
            .Select(endpoint => "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
