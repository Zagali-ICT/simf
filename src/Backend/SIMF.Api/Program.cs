using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SIMF.Api.Authentication;
using SIMF.Api.Endpoints.Admin;
using SIMF.Api.Middleware;
using SIMF.Api.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Domain.Auditing;
using SIMF.Infrastructure;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Structured logging through Serilog (SIMF-SAD-001 section 11).
// P6 — per-project log files under {Storage:LogDirectory}/SIMF.Api/log-{Date}.log;
// the CP /admin/logs page reads from the same root.
builder.Host.UseSerilog((context, configuration) =>
{
    var logDir = context.Configuration["Storage:LogDirectory"] ?? "logs";
    var appName = context.HostingEnvironment.ApplicationName ?? "SIMF.Api";
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

// Database contexts, ASP.NET Core Identity, repositories, email, the audit log.
builder.Services.AddInfrastructure(builder.Configuration);

// The audit log reads the request context; the API supplies it from HttpContext.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();

// H26 — D-086: in-memory cache backs the per-IP bearer-rejection
// throttle in JwtBearerSetup.AuditRejectionAsync so an attacker
// flooding bearer-garbage requests cannot drive synchronous DB
// audit writes per request.
builder.Services.AddMemoryCache();

// Rate limiting for the authentication endpoints — a fixed window per client IP.
var rateLimitOptions =
    builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
    ?? new RateLimitOptions();

builder.Services.AddRateLimiter(rateLimiter =>
{
    // H29 — D-088: every request gets a per-IP fixed-window cap. Closes
    // the post-R3 review's Security SEV-2.1 main finding — pre-H29
    // bearer-protected routes had no per-IP cap, so a malformed-bearer
    // flood (or any other endpoint hit hard from one IP) could pin a
    // CPU core. The global cap is permissive (600/min/IP by default —
    // way above legitimate traffic) and stacks with the per-route
    // "auth" + "auth-email" caps for credential flows; both must pass.
    rateLimiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.GlobalPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.GlobalWindowSeconds),
            }));

    rateLimiter.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            // H7 — D-062: still one shared bucket for null-IP traffic
            // (the original `?? "unknown"` behaviour). The reviewer's
            // concern was a misrouted-no-IP flood sharing with legitimate
            // clients; with the per-email partition below, credential
            // stuffing against a single account is now bounded
            // independent of the IP key — so tightening "unknown"
            // further was deferred to avoid breaking environments
            // (notably ASP.NET TestServer) where no Connection.RemoteIpAddress
            // is set on any request. Revisit if a separate production
            // signal shows misrouted traffic abusing this fallback.
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.PermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
            }));

    // H7 — D-062: per-email partition for the credential-touching paths.
    // EmailRateLimitKeyMiddleware stashes the normalised email on the
    // HttpContext when the request is on a known credential path; this
    // policy keys its window on that. Other endpoints fall through to a
    // permissive partition (no-op), so chaining the policy on a route
    // that does not carry an email is harmless.
    rateLimiter.AddPolicy("auth-email", httpContext =>
    {
        var email = httpContext.Items[EmailRateLimitKeyMiddleware.ItemsKey] as string;
        if (string.IsNullOrEmpty(email))
        {
            return RateLimitPartition.GetNoLimiter<string>("no-email");
        }
        return RateLimitPartition.GetFixedWindowLimiter(
            "email:" + email,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.EmailPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.EmailWindowSeconds),
            });
    });

    // D-179 (gap doc G12 hardening) — per-admin partition on the AI
    // prompt dry-run endpoint. The existing per-IP "auth" window does
    // not protect against an office shared by multiple admins, or a
    // stolen-credential botnet rotating IPs. Partitioned on the JWT
    // `sub` claim so each admin gets their own bucket regardless of
    // source IP.
    //
    // D-181 (review-pass hardening): the no-sub partition used to be
    // `GetNoLimiter` — fine in normal operation (Administrator policy
    // ensures `sub` is present) but the silent zero-cap behaviour
    // masked any JWT-claim-mapper regression. Replaced with a tight
    // fixed-window so an unexpected no-sub request gets bounded
    // automatically instead of disappearing into an unbounded bucket.
    rateLimiter.AddPolicy("ai-test", httpContext =>
    {
        var sub = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(sub))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                "ai-test:no-sub",
                _ => new FixedWindowRateLimiterOptions
                {
                    // D-181 — defense-in-depth: tight cap so a claim-mapper
                    // regression cannot silently kill the rate-limit guard.
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                });
        }
        return RateLimitPartition.GetFixedWindowLimiter(
            "ai-test:" + sub,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.AiTestPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.AiTestWindowSeconds),
            });
    });

    rateLimiter.OnRejected = async (context, cancellationToken) =>
    {
        var http = context.HttpContext;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            http.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        // Record the rejection — the control that stops abuse must leave a trace.
        await http.RequestServices.GetRequiredService<IAuditLog>().WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.RateLimitRejected,
                Outcome = AuditOutcome.Failure,
                ErrorCode = ErrorCodes.RateLimitExceeded,
                Detail = http.Request.Path,
            },
            cancellationToken);

        await http.Response.WriteAsJsonAsync(
            ApiResult<object>.Fail(new ApiError
            {
                Code = ErrorCodes.RateLimitExceeded,
                Message = "Too many requests. Please try again shortly.",
                MessageArabic = "عدد الطلبات كبير. حاول مرة أخرى بعد قليل.",
            }),
            cancellationToken);
    };
});

// FastEndpoints and the OpenAPI document.
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(options =>
{
    options.DocumentSettings = settings =>
    {
        settings.Title = "SIMF API";
        settings.Version = "v1";
    };
});

// Readiness checks (SIMF-OPS-001 Amendment A.4).
builder.Services.AddHealthChecks();

// JWT signing settings. The key must be present and long enough for HMAC-SHA256
// — a missing or weak key would let an attacker forge tokens.
var jwtOptions =
    builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be configured and at least 32 bytes long.");
}

// R1 — D-074: the AvatarBase boot-time gate moved into the StorageOptions
// ValidateOnStart hook inside AddInfrastructure. No raw IConfiguration
// read here any more. The other Storage keys are validated by each
// consumer's constructor — see FilesystemAvatarStorage,
// EncryptedUserIdDocumentStorage, LogFileService.

// Bearer authentication — validates the access token on a protected endpoint
// (see JwtBearerSetup for the hardened parameters and the security-stamp check).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => JwtBearerSetup.Configure(options, jwtOptions));

// The Administrator-only policy is the gate for the admin-reset endpoint
// (D-041). Add new role/permission policies here as more admin actions land.
builder.Services
    .AddAuthorizationBuilder()
    .AddSimfAuthorization();

// The reverse-proxy allowlist — the rate limiter and the audit-log source IP
// depend on it, so an X-Forwarded-For header is honoured only from a trusted
// proxy. Outside Development and the test host it must be configured.
var knownProxies =
    builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];

var app = builder.Build();

if (!app.Environment.IsDevelopment()
    && !app.Environment.IsEnvironment("Testing")
    && knownProxies.Length == 0)
{
    throw new InvalidOperationException(
        "ReverseProxy:KnownProxies must be configured outside Development — "
        + "the rate limiter and the audit-log source IP depend on a trusted proxy.");
}

// Apply the migrations and seed the super-admin. Skipped under the test host,
// which prepares its own database.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    await services.GetRequiredService<SimfIdentityDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<SimfAppDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<IdentitySeeder>().SeedAsync();
}

// Recover the real client IP — but only from a trusted proxy (see above).
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1,
};
forwardedHeaders.KnownProxies.Clear();
forwardedHeaders.KnownIPNetworks.Clear();
foreach (var proxy in knownProxies)
{
    if (IPAddress.TryParse(proxy, out var address))
    {
        forwardedHeaders.KnownProxies.Add(address);
    }
}

app.UseForwardedHeaders(forwardedHeaders);

// The correlation id is established next, so every log line for the request —
// including a failure — carries it.
app.UseMiddleware<CorrelationIdMiddleware>();

// Error handling wraps the rest of the pipeline (SIMF-Sprint1 plan section 7).
app.UseMiddleware<ErrorHandlingMiddleware>();

// H7 — D-062: peek the request body for the email field on credential
// paths so the "auth-email" rate-limit policy can key its partition on
// it. Must run BEFORE UseRateLimiter so the key is set when the limiter
// reads it.
app.UseMiddleware<EmailRateLimitKeyMiddleware>();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "api/v1";

    // Field-validation failures use the standard ApiResult shape (API-001 §6-7).
    // Each FluentValidation rule attaches Arabic as its CustomState via the
    // .Bilingual(en, ar) extension (D-030 / myComment #14).
    config.Errors.ResponseBuilder = (failures, _, _) =>
        ApiResult<object>.Fail(new ApiError
        {
            Code = ErrorCodes.ValidationFailed,
            Message = "One or more fields are invalid.",
            MessageArabic = "يوجد حقل أو أكثر غير صالح.",
            Details = failures
                .Select(failure => new ApiErrorDetail
                {
                    Field = failure.PropertyName,
                    Message = failure.ErrorMessage,
                    MessageArabic = failure.CustomState as string ?? failure.ErrorMessage,
                })
                .ToList(),
        });
});

// The OpenAPI UI is available outside production only (SIMF-API-001 section 13).
if (!app.Environment.IsProduction())
{
    app.UseSwaggerGen();
}

// The readiness endpoint (SIMF-OPS-001 Amendment A.4).
app.MapHealthChecks("/health");

app.Run();

/// <summary>
/// Declared so the integration tests can host the API with
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
