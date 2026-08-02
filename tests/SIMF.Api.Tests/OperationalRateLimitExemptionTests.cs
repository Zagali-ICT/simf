using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common.Options;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// D-819 / D-822 — the on-site operational surface is deliberately UNLIMITED, and
/// that exemption is deliberately gated on a bearer token.
///
/// <para><b>Why this file exists.</b> The owner's requirement was emphatic and
/// specific: a rate limit on the admin/operational API "will make system down".
/// It is not a preference — the Control Panel is a server-side BFF, so from the
/// API's view every CP desk shares one source IP, and the old <c>"auth"</c> cap
/// of 20 requests per minute applied to the WHOLE Control Panel. At a venue with
/// a queue at the door that is an outage, not a safety margin.</para>
///
/// <para>Until this file, none of that was pinned. Nothing failed if someone
/// "hardened" a gate endpoint by putting <c>RequireRateLimiting("auth")</c> back
/// on it, and nothing failed if a new desk endpoint shipped without the
/// exemption — both of which throttle the gates on event day, and neither of
/// which any existing test could see. A grep for <c>OperationalPolicy</c> across
/// <c>tests/</c> returned nothing at all.</para>
///
/// <para>The set is asserted in BOTH directions on purpose. A missing entry is an
/// availability defect (an operational route silently capped); an unexpected one
/// is a security decision (a route opted out of the global per-IP cap) that
/// should be argued for rather than merged quietly.</para>
/// </summary>
public sealed class OperationalRateLimitExemptionTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public OperationalRateLimitExemptionTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    /// <summary>
    /// The reviewed allow-list: every route that may run unthrottled, and why.
    ///
    /// <para>Each is an authenticated operator action performed at speed while a
    /// queue waits, and each is gated on a specific permission — which is the
    /// real control here, not the limiter.</para>
    /// </summary>
    private static readonly Dictionary<string, string> ExpectedOperationalRoutes = new(
        StringComparer.OrdinalIgnoreCase)
    {
        // The gate itself. A scan per person through the door.
        ["/api/v1/app/gates/{gateId:guid}/scans"] = "Gates.Operate — the door scan.",
        ["/api/v1/app/gates/{gateId:guid}/visitors/list"] = "Gates.Operate — the operator's roster lookup.",
        ["/api/v1/app/gates/offline-config"] = "Gates.Operate — cached rules + badge key for an offline gate.",

        // The desks. One call per walk-in, in bursts.
        ["/api/v1/admin/visitors/register-onsite"] = "Visitors.RegisterOnsite — CP walk-in desk.",
        ["/api/v1/admin/others/register-onsite"] = "Others.RegisterOnsite — CP walk-in desk (partner tiers).",
        ["/api/v1/app/staff/visitors/register-onsite"] = "Staff app walk-in desk.",
        ["/api/v1/app/staff/visitors/{id:guid}/id-document"] = "Staff app — the ID capture that follows a walk-in.",
        ["/api/v1/app/staff/visitors/{id:guid}/avatar"] = "Staff app — the photo that follows a walk-in.",
        ["/api/v1/app/staff/sessions/{sessionId:guid}/seating/by-badge"] = "Staff app — seating a queue at a hall door.",

        // The approval queue, worked in bulk on the morning of the event.
        ["/api/v1/admin/visitors/bulk-approve"] = "Visitors.Approve — bulk queue.",
        ["/api/v1/admin/others/bulk-approve"] = "Others.Approve — bulk queue.",
        ["/api/v1/admin/visitors/bulk-reject"] = "Visitors.Approve — bulk queue.",
        ["/api/v1/admin/others/bulk-reject"] = "Others.Approve — bulk queue.",

        // Reconciling an offline desk's shift, which is one call per badge issued.
        ["/api/v1/admin/offline/batch"] = "D-820 — offline badge upload.",
    };

    [Fact]
    public void The_unthrottled_surface_is_exactly_the_reviewed_allow_list()
    {
        var exempt = _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
                == RateLimitOptions.OperationalPolicy)
            .Select(endpoint => "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // A sweep that matches nothing passes silently. If the enumeration ever
        // breaks, this says so instead of reporting a clean bill of health.
        Assert.True(
            exempt.Count > 0,
            "No endpoint declared the operational policy at all — the enumeration "
            + "is broken, not the exemption.");

        var unexpected = exempt
            .Where(path => !ExpectedOperationalRoutes.ContainsKey(path))
            .ToList();
        var missing = ExpectedOperationalRoutes.Keys
            .Where(path => !exempt.Contains(path, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Operational endpoint(s) LOST the rate-limit exemption. On event day "
            + "these fall back to the per-IP cap, and because the Control Panel is "
            + "a server-side BFF every desk shares one IP — so the whole Control "
            + "Panel gets capped together and the queue stops:\n  "
            + string.Join("\n  ", missing));
        Assert.True(
            unexpected.Count == 0,
            "Endpoint(s) newly opted OUT of the global per-IP cap. That is a "
            + "security decision, not a performance tweak: an authenticated flood "
            + "on these is unbounded. Justify and add to "
            + $"ExpectedOperationalRoutes:\n  {string.Join("\n  ", unexpected)}");
    }

    [Fact]
    public void Every_unthrottled_endpoint_still_demands_a_permission()
    {
        // The exemption's whole rationale is "the permission gate is the real
        // control here". If one of these were ever AllowAnonymous, that sentence
        // would be false and the route would be an unauthenticated, unlimited
        // entry point — the worst combination in the system.
        var anonymousAndUnlimited = _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
                    == RateLimitOptions.OperationalPolicy
                && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(
            anonymousAndUnlimited.Count == 0,
            "Endpoint(s) are BOTH anonymous AND exempt from every rate limit — "
            + "an unauthenticated caller can hit them as fast as the network "
            + "allows:\n  " + string.Join("\n  ", anonymousAndUnlimited));
    }

    [Fact]
    public void No_unthrottled_endpoint_also_carries_a_limiting_policy()
    {
        // Chained limiters STACK — every one of them must admit the request. The
        // credential routes rely on that deliberately (D-062: "auth" per-IP plus
        // "auth-email" per-email, both must pass), so more than one policy on a
        // route is normal and must NOT be flagged.
        //
        // Combining the operational policy with a limiting one is the case that
        // cannot be intentional: one declares the route unlimited, the other caps
        // it, and because they stack the cap simply wins. The route then reads as
        // exempt at the call site while being throttled at run time — the exact
        // shape that takes a gate down on event day and survives code review,
        // because each declaration looks right on its own line.
        var contradictory = _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Path = "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/'),
                Policies = endpoint.Metadata
                    .OfType<EnableRateLimitingAttribute>()
                    .Select(attribute => attribute.PolicyName)
                    .Where(name => name is not null)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
            })
            .Where(entry =>
                entry.Policies.Contains(RateLimitOptions.OperationalPolicy, StringComparer.Ordinal)
                && entry.Policies.Count > 1)
            .Select(entry => $"{entry.Path}  ({string.Join(" + ", entry.Policies)})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(
            contradictory.Count == 0,
            "Endpoint(s) declare the UNLIMITED operational policy alongside a "
            + "limiting one. Chained limiters stack, so the cap wins and the route "
            + "is throttled despite reading as exempt:\n  "
            + string.Join("\n  ", contradictory));
    }
}

/// <summary>
/// D-822 — the exemption is granted only to a request that CARRIES a bearer.
///
/// <para><c>UseRateLimiter</c> runs before <c>UseAuthentication</c>, so when the
/// global limiter decides whether to exempt a request, <c>HttpContext.User</c> is
/// still empty and the permission gate the exemption leans on has not run. Without
/// the header check, an ANONYMOUS flood against an operational route bypassed even
/// the global per-IP cap — re-opening the finding the global limiter exists to
/// close.</para>
///
/// <para>Its own factory because the global cap has to be small enough to trip;
/// the shared one pins it at a million so ordinary classes never hit it.</para>
/// </summary>
public sealed class GlobalCapApiFactory : SimfApiFactory
{
    public const int GlobalPermitLimit = 5;

    public GlobalCapApiFactory() =>
        Environment.SetEnvironmentVariable(
            "RateLimit__GlobalPermitLimit",
            GlobalPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

public sealed class OperationalExemptionRequiresABearerTests
    : IClassFixture<GlobalCapApiFactory>, IDisposable
{
    private const string OperationalRoute = "/api/v1/app/gates/11111111-1111-1111-1111-111111111111/scans";
    private const int BurstSize = GlobalCapApiFactory.GlobalPermitLimit * 3;

    private readonly HttpClient _client;

    public OperationalExemptionRequiresABearerTests(GlobalCapApiFactory factory)
    {
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    // Restore the permissive default the shared factory expects; these vars are
    // process-wide, and a later class that builds its host while this one's small
    // cap is still set would throttle itself for no reason.
    public void Dispose() =>
        Environment.SetEnvironmentVariable("RateLimit__GlobalPermitLimit", "1000000");

    [Fact]
    public async Task An_anonymous_flood_on_an_operational_route_is_still_capped()
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < BurstSize; i++)
        {
            using var response = await _client.PostAsJsonAsync(
                OperationalRoute, new { qr = "X", idempotencyKey = Guid.NewGuid().ToString(), source = 1 });
            statuses.Add(response.StatusCode);
        }

        Assert.True(
            statuses.Contains(HttpStatusCode.TooManyRequests),
            "An anonymous burst of " + BurstSize + " on an operational route was "
            + "never throttled, so the exemption is being granted BEFORE "
            + "authentication. Anyone on the network can flood the gate routes "
            + $"without a token. Statuses: {string.Join(",", statuses.Select(s => (int)s))}");
    }

    [Fact]
    public async Task The_same_flood_carrying_a_bearer_is_never_throttled()
    {
        // Deliberately a GARBAGE bearer. The check runs before authentication, so
        // it cannot tell a real token from a fake one — it only requires that one
        // was sent. That is the documented limit of the guard, and pinning it here
        // stops someone "tightening" it into something that cannot work this early
        // in the pipeline. Every genuine operator request carries a header, so the
        // owner's no-limit-at-the-gates requirement is untouched.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < BurstSize; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, OperationalRoute)
            {
                Content = JsonContent.Create(new
                {
                    qr = "X",
                    idempotencyKey = Guid.NewGuid().ToString(),
                    source = 1,
                }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");
            using var response = await _client.SendAsync(request);
            statuses.Add(response.StatusCode);
        }

        Assert.True(
            !statuses.Contains(HttpStatusCode.TooManyRequests),
            "A bearer-carrying burst on an operational route WAS throttled. This is "
            + "the requirement the owner called critical: the Control Panel is a "
            + "server-side BFF, so every desk shares one source IP and a per-IP cap "
            + $"stops the whole venue at once. Statuses: {string.Join(",", statuses.Select(s => (int)s))}");
    }
}
