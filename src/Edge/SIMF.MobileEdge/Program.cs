// The mobile presentation tier.
//
// The Flutter app compiles its API base URL in (BuildConfig.apiBaseUrl defaults
// to https://api.simrsnf.com/api/v1), so the client's endpoint IS the
// application tier and no firewall changes that. Putting a transparent proxy in
// front of a published API does not add a tier either; the client still
// addresses the API.
//
// This host is that tier. It is published at edge.simrsnf.com and forwards only
// the mobile surface inward, which is what lets the API stop being published to
// the internet: api.simrsnf.com stays the API's own name and resolves inside the
// estate only.
//
// The cost of using a separate name, stated where it cannot be missed: the app's
// base URL is compile-time, so an installed build still talks to the API
// directly and knows nothing about this host. Routing mobile traffic here needs
// a rebuild with --dart-define and a store release on both platforms, and
// withdrawing the API's public DNS record has to wait for that release to land.
//
// It deliberately does almost nothing: no reshaping, no aggregation, no
// business logic. The shipped mobile wire contract is append-only (D-219), and
// every field the app decodes has to survive this hop byte for byte.
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Production config arrives as SIMF_EDGE_-prefixed Machine-scope environment
// variables (deploy/set-env-edge.template.ps1). This source strips the prefix,
// so SIMF_EDGE_ReverseProxy__Clusters__api__Destinations__primary__Address binds
// through to the YARP cluster below. ASPNETCORE_ENVIRONMENT stays un-prefixed:
// the host reads it before configuration sources load.
//
// The prefix is PER APPLICATION. It matters most here: the API reads
// ReverseProxy:KnownProxies too, and under one shared namespace this host and
// the API could not name different upstreams on the same box - the edge's
// upstream is the load balancer, the API's includes the edge.
builder.Configuration.AddEnvironmentVariables("SIMF_EDGE_");

// A server provisioned before the per-application prefixes still carries the old
// SIMF_ variables, which this build does not read. Without this the edge would
// report its destination address missing while the value sits in the machine
// environment under its former name.
//
// Written out here rather than shared from SIMF.Common: this host deliberately
// carries no project reference, so that an internet-facing proxy does not pull
// in the permission catalogue, the enums and the rest of the domain surface.
// Production only, matching the boot gates below and every other one in the
// estate: the guard is about a half-upgraded SERVER, while a developer box
// legitimately carries whatever its owner has exported.
var legacyNames = builder.Environment.IsProduction()
    ? Environment.GetEnvironmentVariables().Keys.Cast<object>()
        .Select(key => key?.ToString() ?? string.Empty)
        .Where(name => name.StartsWith("SIMF_", StringComparison.Ordinal))
        .Where(name => !name.StartsWith("SIMF_EDGE_", StringComparison.Ordinal))
        .Where(name => !name.StartsWith("SIMF_API_", StringComparison.Ordinal))
        .Where(name => !name.StartsWith("SIMF_CP_", StringComparison.Ordinal))
        .Where(name => !name.StartsWith("SIMF_WEB_", StringComparison.Ordinal))
        .Where(name => !name.StartsWith("SIMF_TEST_", StringComparison.Ordinal))
        .Where(name => !name.StartsWith("SIMF_SMOKE_", StringComparison.Ordinal))
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToList()
    : [];

if (legacyNames.Count > 0)
{
    throw new InvalidOperationException(
        $"This server still carries {legacyNames.Count} environment variable(s) using the "
        + "retired 'SIMF_' prefix, which this build does not read: "
        + string.Join(", ", legacyNames.Take(8))
        + (legacyNames.Count > 8 ? ", ..." : string.Empty)
        + ". Each SIMF application now reads its own prefix ('SIMF_EDGE_' here). "
        + "Re-provision with deploy/set-env-edge.ps1, clear the old variables with "
        + "deploy/clear-env.ps1, then restart the pool.");
}

// Per-project log files under {Storage:LogDirectory}/SIMF.MobileEdge/log-{Date}.log,
// the same shape and retention as the API, Control Panel and Website. This host
// is internet-facing and is the first thing a mobile request touches, so its log
// is where an incident starts; leaving it on default console logging would mean
// nothing survives an app-pool recycle.
builder.Host.UseSerilog((context, configuration) =>
{
    var logDir = context.Configuration["Storage:LogDirectory"] ?? "logs";
    var appName = context.HostingEnvironment.ApplicationName ?? "SIMF.MobileEdge";
    var path = Path.Combine(logDir, appName, "log-.log");
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: path,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 31,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} "
                + "[{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
});

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
            + "SIMF_EDGE_ReverseProxy__Clusters__api__Destinations__primary__Address to the "
            + "API's address inside the estate, which is the only host permitted to answer it.");
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
