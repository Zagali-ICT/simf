// The mobile presentation tier.
//
// The Flutter app compiles its API base URL in (BuildConfig.apiBaseUrl defaults
// to https://api.simrsnf.com/api/v1), so the client's endpoint IS the
// application tier and no firewall changes that. Putting a transparent proxy in
// front of a published API does not add a tier either; the client still
// addresses the API.
//
// This host is that tier. It takes over the public api.simrsnf.com name, so the
// installed app needs no rebuild and no store release, and forwards only the
// mobile surface to an API that is no longer published at all.
//
// It deliberately does almost nothing: no reshaping, no aggregation, no
// business logic. The shipped mobile wire contract is append-only (D-219), and
// every field the app decodes has to survive this hop byte for byte.
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Production config arrives as SIMF_-prefixed Machine-scope environment
// variables (deploy/set-env.template.ps1). This source strips the prefix, so
// SIMF_ReverseProxy__Clusters__api__Destinations__primary__Address binds
// through to the YARP cluster below. ASPNETCORE_ENVIRONMENT stays un-prefixed:
// the host reads it before configuration sources load.
builder.Configuration.AddEnvironmentVariables("SIMF_");

// The whole point of this host. Routes and clusters are configuration, not code,
// so a new mobile endpoint needs no rebuild - but the published path set stays a
// deliberate allow-list, which is what keeps the admin surface unreachable here.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// This host sits behind the WAF and load balancer, so the client address arrives
// in X-Forwarded-For. The API rate-limits and audits on that address; if it is
// not forwarded, every mobile request appears to come from this one host and the
// per-caller limits collapse into a single shared bucket.
var knownProxies =
    builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];

var app = builder.Build();

// Refuse to start rather than forward to nowhere, or trust an unverified
// X-Forwarded-For. Both are silent failures otherwise: the first returns 502 to
// every app user, the second makes the API's rate limiter trivially bypassable
// by any client willing to set a header.
if (!app.Environment.IsDevelopment())
{
    if (knownProxies.Length == 0)
    {
        throw new InvalidOperationException(
            "ReverseProxy:KnownProxies must be configured outside Development - "
            + "an unverified X-Forwarded-For lets any caller spoof its source address "
            + "past the API's rate limiter and into the audit log.");
    }

    var destination = app.Configuration[
        "ReverseProxy:Clusters:api:Destinations:primary:Address"];

    if (string.IsNullOrWhiteSpace(destination))
    {
        throw new InvalidOperationException(
            "The api cluster has no destination address. Set "
            + "SIMF_ReverseProxy__Clusters__api__Destinations__primary__Address to the "
            + "PRIVATE address of the API, which is the only host permitted to answer it.");
    }
}

var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};

// Cleared first: the defaults trust loopback, which is not the proxy in front of
// this host and would accept a forged header from anything on the box.
forwardedHeaders.KnownProxies.Clear();
forwardedHeaders.KnownIPNetworks.Clear();
foreach (var proxy in knownProxies)
{
    if (System.Net.IPAddress.TryParse(proxy, out var address))
    {
        forwardedHeaders.KnownProxies.Add(address);
    }
}

app.UseForwardedHeaders(forwardedHeaders);

// Baseline security response headers, matching the API and both Blazor hosts
// (NCA App-Sec Standard A3-4 / A5-13 / A6-21). This host serves no HTML and is
// never framed, so the policy can be the strict one rather than report-only.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

// Answers the load balancer directly. It deliberately does NOT probe the API:
// this endpoint reports whether the edge is up, and a health check that fails
// because a downstream is unhealthy takes the edge out of rotation for a fault
// it cannot fix, turning one outage into two.
app.MapGet("/health", () => Results.Ok("healthy"));

app.MapReverseProxy();

app.Run();
