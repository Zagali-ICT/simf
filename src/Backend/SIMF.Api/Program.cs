using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SIMF.Api.Authentication;
using SIMF.Api.Authorization;
using SIMF.Infrastructure.Operations;
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

// Production secrets/config arrive as SIMF_API_-prefixed Machine-scope
// environment variables (deploy/set-env-api.template.ps1). This source strips
// the prefix, so SIMF_API_ConnectionStrings__SimfAppDb binds to
// ConnectionStrings:SimfAppDb. ASPNETCORE_ENVIRONMENT stays un-prefixed (the
// host reads it before configuration sources load).
//
// The prefix is PER APPLICATION. Machine scope is shared by every process on the
// box, so while all four hosts read one common "SIMF_" the estate could not give
// two of them different values for the same key: there was a single
// SIMF_Storage__LogDirectory and a single SIMF_Api__BaseUrl between them, shared
// whether that was wanted or not. On a server running more than one SIMF app
// that is a collision with no way out. A prefix per app removes it.
//
// Note what it is not: a security boundary. Any process on the box can still
// read any variable whatever it is called, which is why the real isolation is
// that each server receives only its own package's values.
builder.Configuration.AddEnvironmentVariables("SIMF_API_");

// Fail loudly on a half-finished upgrade. A server provisioned before the
// per-application prefixes still carries SIMF_-prefixed variables, which this
// build no longer reads: without this the host would boot, find its boot gates
// missing, and report that an encryption key is unset while the value sits right
// there in the machine environment under the old name.
SimfLegacyEnvironmentGuard.Verify("SIMF_API_", builder.Environment.IsProduction());

// Structured logging through Serilog. Per-project log files live under
// {Storage:LogDirectory}/SIMF.Api/log-{Date}.log; the CP /admin/logs page reads
// from the same root.
builder.Host.UseSerilog((context, configuration) =>
{
    var logDir = context.Configuration["Storage:LogDirectory"] ?? "logs";
    var appName = context.HostingEnvironment.ApplicationName ?? "SIMF.Api";
    var path = Path.Combine(logDir, appName, "log-.log");
    // Background-worker logs go to their OWN per-project folder (SIMF.Workers)
    // so the CP /admin/logs viewer lists them apart from the API request logs,
    // and they are excluded from the API folder to keep that view clean. The
    // worker/API split keys off the Serilog SourceContext (the logger's type).
    var workerPath = Path.Combine(logDir, "SIMF.Workers", "log-.log");
    const string logTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} "
        + "[{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    // The exact hosted-worker types whose logs go to the SIMF.Workers folder.
    // Matching by full type name (not namespace prefix) keeps non-worker types
    // that share these namespaces (e.g. EmailQueue, OperationsToggleService) in
    // the API log, where the request path that triggered them can be traced.
    var workerSources = new HashSet<string>(StringComparer.Ordinal)
    {
        "SIMF.Infrastructure.Operations.RegistrationGateAutoCloseWorker",
        "SIMF.Infrastructure.Operations.SessionReminderWorker",
        "SIMF.Infrastructure.Operations.MeetingAwaitingSpeakerExpiryWorker",
        "SIMF.Infrastructure.Operations.ReservationNoShowReleaseWorker",
        "SIMF.Infrastructure.Operations.SessionRatingPromptWorker",
        "SIMF.Infrastructure.Operations.ProgrammeRatingPromptWorker",
        "SIMF.Infrastructure.Operations.HallAttendanceCloseoutWorker",
        "SIMF.Infrastructure.Email.EmailBackgroundService",
        "SIMF.Api.HostedServices.RetentionSweepWorker",
        "SIMF.Api.HostedServices.DormantAccountSweepService",
    };

    bool IsWorkerLog(Serilog.Events.LogEvent e) =>
        e.Properties.TryGetValue("SourceContext", out var value)
        && value is Serilog.Events.ScalarValue { Value: string source }
        && workerSources.Contains(source);

    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        // API log: everything except the background workers.
        .WriteTo.Logger(api => api
            .Filter.ByExcluding(IsWorkerLog)
            .WriteTo.File(
                path: path,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                outputTemplate: logTemplate))
        // Background-worker log: a separate SIMF.Workers folder the CP viewer lists on its own.
        .WriteTo.Logger(workers => workers
            .Filter.ByIncludingOnly(IsWorkerLog)
            .WriteTo.File(
                path: workerPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                outputTemplate: logTemplate));
});

// Database contexts, ASP.NET Core Identity, repositories, email, the audit log.
builder.Services.AddInfrastructure(builder.Configuration);

// NCA account-dormancy control — daily dormant-account disable sweep (no-op
// until configured). Leased: it is a daily database sweep, so on a multi-instance
// estate exactly one node must run it (see WorkerLeaseRegistration).
builder.Services.AddLeasedHostedService<SIMF.Api.HostedServices.DormantAccountSweepService>();

// NCA data-minimisation — daily retention purge of dead security artifacts.
// Leased for the same reason: a purge that four instances run concurrently
// deletes the same rows four times over and races itself.
builder.Services.AddLeasedHostedService<SIMF.Api.HostedServices.RetentionSweepWorker>();

// The audit log reads the request context; the API supplies it from HttpContext.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();
// The hero video's absolute URL is composed at read time now, and only the API
// can see the request its origin falls back to.
builder.Services.AddScoped<SIMF.Application.Abstractions.IPublicApiOriginProvider,
    SIMF.Api.Infrastructure.HttpPublicApiOriginProvider>();

// In-memory cache backs the per-IP bearer-rejection
// throttle in JwtBearerSetup.AuditRejectionAsync so an attacker
// flooding bearer-garbage requests cannot drive synchronous DB
// audit writes per request.
builder.Services.AddMemoryCache();

// Rate limiting for the authentication endpoints — a fixed window per client IP.
var rateLimitOptions =
    builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
    ?? new RateLimitOptions();

// The X-App-Key gate. No AppKey section anywhere means an empty key list, which
// means the gate is inert — the deliberate default, so that shipping this cannot
// reject a request from a client built before it existed.
var appKeyOptions =
    builder.Configuration.GetSection(AppKeyOptions.SectionName).Get<AppKeyOptions>()
    ?? new AppKeyOptions();

// True when the resolved endpoint declares the operational
// rate-limit policy. Reads the metadata that RequireRateLimiting(...) attaches,
// so the global-limiter exemption is derived from the endpoints themselves and
// cannot drift from them. Returns false when routing has not resolved an
// endpoint, which leaves the caller on the per-IP limiter (the safe direction).
static bool IsOperationalEndpoint(HttpContext httpContext) =>
    httpContext.GetEndpoint()?.Metadata
        .GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
        == RateLimitOptions.OperationalPolicy
    // The exemption ALSO requires a bearer token on the request.
    //
    // UseRateLimiter runs BEFORE UseAuthentication (see the pipeline below), so
    // at this point httpContext.User is empty and the permission gate the
    // exemption's rationale leans on has not run yet. Without this check an
    // ANONYMOUS flood against an operational route bypassed the global per-IP
    // cap entirely — re-opening the very hole the global limiter exists to
    // close.
    //
    // A header check is all that is available this early. It is not
    // authentication: a garbage bearer still reaches the exemption. It does mean
    // an attacker must send one, and every genuine operator request carries one,
    // so the owner's "no rate limit at the gates" requirement is untouched.
    && httpContext.Request.Headers.Authorization.Count > 0;

builder.Services.AddRateLimiter(rateLimiter =>
{
    // Every request gets a per-IP fixed-window cap. Bearer-protected
    // routes previously had no per-IP cap, so a malformed-bearer
    // flood (or any other endpoint hit hard from one IP) could pin a
    // CPU core. The global cap is permissive (600/min/IP by default —
    // way above legitimate traffic) and stacks with the per-route
    // "auth" + "auth-email" caps for credential flows; both must pass.
    rateLimiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext =>
        {
            // The on-site operational endpoints opt out of the global
            // per-IP cap as well as the "auth" cap. Exempting them at the
            // policy level alone would not be enough: the Control Panel is a
            // server-side BFF, so every CP desk shares the CP host's IP and
            // would still collide inside this global bucket.
            //
            // Detected from endpoint metadata rather than a path list so the
            // exemption cannot drift from the endpoints that declare it. If
            // routing has not resolved an endpoint yet, this falls through to
            // the per-IP limiter, which is the safe direction.
            if (IsOperationalEndpoint(httpContext))
            {
                return RateLimitPartition.GetNoLimiter<string>("operational");
            }

            return RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitOptions.GlobalPermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitOptions.GlobalWindowSeconds),
                });
        });

    // The on-site operational surface: gate scans, hall arrivals,
    // walk-in registration, approve / bulk-approve, staff uploads and the
    // offline batch upload. Deliberately unlimited; see
    // RateLimitOptions.OperationalPolicy for the full rationale. Every one of
    // these endpoints is gated on an authenticated operator's permission, which
    // is the real control here — EXCEPT that the permission check runs after
    // this middleware, which is why IsOperationalEndpoint also requires an
    // Authorization header before granting the exemption.
    rateLimiter.AddPolicy(
        RateLimitOptions.OperationalPolicy,
        _ => RateLimitPartition.GetNoLimiter<string>("operational"));

    // Signed-in reference-data lookups a UI queries repeatedly. Kept OFF the
    // "auth" policy: that one is the credential limiter, and a type-ahead
    // sharing its 20/minute bucket spends the visitor's own sign-in budget.
    rateLimiter.AddPolicy(
        RateLimitOptions.LookupPolicy,
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.LookupPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.LookupWindowSeconds),
            }));

    rateLimiter.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Still one shared bucket for null-IP traffic
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

    // Per-email partition for the credential-touching paths.
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

    // Per-admin partition on the AI
    // prompt dry-run endpoint. The existing per-IP "auth" window does
    // not protect against an office shared by multiple admins, or a
    // stolen-credential botnet rotating IPs. Partitioned on the JWT
    // `sub` claim so each admin gets their own bucket regardless of
    // source IP.
    //
    // The no-sub partition used to be
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
                    // Defense-in-depth: tight cap so a claim-mapper
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

    // The Control Panel help assistant (POST /admin/ai/assistant) hits the AI
    // provider on every question. Same per-sub partition rationale as "ai-test"
    // (the per-IP "auth" window cannot bound a shared office or an IP-rotating
    // botnet), with the same tight no-sub fallback; a slightly higher permit
    // count because the assistant is used interactively across CP pages.
    rateLimiter.AddPolicy("ai-assistant", httpContext =>
    {
        var sub = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(sub))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                "ai-assistant:no-sub",
                _ => new FixedWindowRateLimiterOptions
                {
                    // Defense-in-depth: tight cap so a claim-mapper regression
                    // cannot silently kill the rate-limit guard.
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                });
        }
        return RateLimitPartition.GetFixedWindowLimiter(
            "ai-assistant:" + sub,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.AiAssistantPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.AiAssistantWindowSeconds),
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

// FastEndpoints and the OpenAPI documents. The App↔CP split emits
// TWO OpenAPI documents from the one API so a mobile/App developer reads only
// the App surface and a CP developer reads only the admin surface. The filter
// keys off the route segment — every App route is under /app/* and every CP
// route under /admin/*.
builder.Services.AddFastEndpoints();

// Output caching for the anonymous public read endpoints. The "PublicRead"
// policy caches for 45s and varies by ALL query keys ("*") so paged / filtered /
// ?day= variants never share an entry. Under the Testing environment the policy
// is a no-op AND the middleware below is not added, so the integration suite
// always reads fresh (every create → public-read → mutate → public-read stays
// correct). Framework-provided by the Web SDK — no package reference. The service
// is registered unconditionally because app.UseOutputCache() throws if it is
// missing; only the middleware is environment-gated.
builder.Services.AddOutputCache(options =>
    options.AddPolicy("PublicRead", policy =>
    {
        if (builder.Environment.IsEnvironment("Testing"))
        {
            policy.NoCache();
        }
        else
        {
            policy.Expire(TimeSpan.FromSeconds(45)).SetVaryByQuery("*");
        }
    }));

builder.Services.SwaggerDocument(options =>
{
    options.DocumentSettings = settings =>
    {
        settings.Title = "SIMF App API";
        settings.DocumentName = "app";
        settings.Version = "v1";
    };
    // Explicit so a future FastEndpoints default change can't silently drop the
    // "Authorize" (JWT bearer) button — every protected endpoint authenticates
    // on the default Bearer scheme.
    options.EnableJWTBearerAuth = true;
    options.EndpointFilter = ep =>
        ep.Routes?.Any(route => route.Contains("app/") && !route.Contains("admin/")) == true;
});
builder.Services.SwaggerDocument(options =>
{
    options.DocumentSettings = settings =>
    {
        settings.Title = "SIMF CP API";
        settings.DocumentName = "cp";
        settings.Version = "v1";
    };
    options.EnableJWTBearerAuth = true;
    options.EndpointFilter = ep =>
        ep.Routes?.Any(route => route.Contains("admin/")) == true;
});

// Readiness checks. The "workers" check reports the
// background-worker tier's health from the in-process heartbeat registry.
builder.Services.AddHealthChecks()
    .AddCheck<SIMF.Api.HealthChecks.WorkersHealthCheck>("workers");

// Web-app CORS. Two origin sources, both explicit allow-lists:
// - Cors:WebAppOrigins — ANY environment; empty/absent = no CORS at all, which
//   is the posture the API had before web-app CORS was introduced. Set it only
//   when the published Flutter web app is hosted on a different origin than
//   this API.
// - Cors:DevWebOrigins — Development only; defaults to localhost:8080 for
//   a local `flutter run -d chrome` diagnostics session.
// Never a wildcard: origins are always named, headers/methods scoped to the
// app's needs by the browser preflight.
const string devWebCorsPolicy = "DevWebCors";
var webAppOrigins =
    builder.Configuration.GetSection("Cors:WebAppOrigins").Get<string[]>()
    ?? [];
if (builder.Environment.IsDevelopment())
{
    var devWebOrigins =
        builder.Configuration.GetSection("Cors:DevWebOrigins").Get<string[]>()
        ?? ["http://localhost:8080"];
    webAppOrigins = [.. webAppOrigins, .. devWebOrigins];
}
var webCorsEnabled = webAppOrigins.Length > 0;
if (webCorsEnabled)
{
    builder.Services.AddCors(options => options.AddPolicy(
        devWebCorsPolicy,
        policy => policy.WithOrigins(webAppOrigins).AllowAnyHeader().AllowAnyMethod()));
}

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

// The AvatarBase boot-time gate moved into the StorageOptions
// ValidateOnStart hook inside AddInfrastructure. No raw IConfiguration
// read here any more. The other Storage keys are validated by each
// consumer's constructor — see FilesystemAvatarStorage,
// EncryptedUserIdDocumentStorage, LogFileService.

// Bearer authentication — validates the access token on a protected endpoint
// (see JwtBearerSetup for the hardened parameters and the security-stamp check).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => JwtBearerSetup.Configure(options, jwtOptions))
    // A second scheme for short-lived recording-stream
    // tokens (distinct audience, query-string token, no security-stamp check).
    .AddJwtBearer(JwtBearerSetup.StreamScheme,
        options => JwtBearerSetup.ConfigureStream(options, jwtOptions));

// The Administrator-only policy is the gate for the admin-reset endpoint.
// Add new role/permission policies here as more admin actions land.
builder.Services
    .AddAuthorizationBuilder()
    .AddSimfAuthorization();

// Per-page/per-action permission policies (perm:<code>) are
// materialised on demand by the dynamic provider, and the handler matches
// the required code against the caller's `perm` claims (Administrator's
// wildcard satisfies any code). Registered after the builder so this
// provider wins over the default one for `perm:` policy names.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// The reverse-proxy allowlist — the rate limiter and the audit-log source IP
// depend on it, so an X-Forwarded-For header is honoured only from a trusted
// proxy. Outside Development and the test host it must be configured.
var knownProxies =
    builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];

// OpenAPI UI exposure. Off in production unless explicitly enabled, and
// then only behind the Basic-auth gate below.
var swaggerOptions =
    builder.Configuration.GetSection(SwaggerOptions.SectionName).Get<SwaggerOptions>()
    ?? new SwaggerOptions();

// Bound for the Production boot guard below — the committed default
// super-admin password must never be the live credential.
var superAdminOptions =
    builder.Configuration.GetSection(SuperAdminOptions.SectionName).Get<SuperAdminOptions>()
    ?? new SuperAdminOptions();

var app = builder.Build();

if (!app.Environment.IsDevelopment()
    && !app.Environment.IsEnvironment("Testing")
    && knownProxies.Length == 0)
{
    throw new InvalidOperationException(
        "ReverseProxy:KnownProxies must be configured outside Development — "
        + "the rate limiter and the audit-log source IP depend on a trusted proxy.");
}

// Never expose the OpenAPI UI unauthenticated in production. If it is
// enabled there, the Basic-auth credentials must be present, or refuse to start.
if (app.Environment.IsProduction()
    && swaggerOptions.AllowSwagger
    && (string.IsNullOrWhiteSpace(swaggerOptions.Username)
        || string.IsNullOrWhiteSpace(swaggerOptions.Password)))
{
    throw new InvalidOperationException(
        "Swagger:Username and Swagger:Password must be configured when "
        + "Swagger:AllowSwagger is true in Production — the OpenAPI UI must not "
        + "be served without the Basic-auth gate.");
}

// Refuse to start in Production with the committed default super-admin
// password — it must be overridden via SIMF_API_SuperAdmin__TempPassword
// (docs/security/SIMF-Security-Assessment-2026-06-20.md).
if (app.Environment.IsProduction()
    && superAdminOptions.TempPassword == "Aa@123456789")
{
    throw new InvalidOperationException(
        "SuperAdmin:TempPassword is the committed default — configure a real "
        + "SIMF_API_SuperAdmin__TempPassword before starting in Production.");
}

// Held-item #2b — refuse to start in Production without a super-admin TOTP seed.
//
// The guard above stops a KNOWN-BAD password reaching production. This one stops
// the bootstrap super-admin being single-factor at all: with no seed configured,
// IdentitySeeder writes no AuthenticatorKey, SignInService's second-factor
// selection has nothing to challenge against, and the most privileged account in
// the system — the one whose permission claim is the wildcard "*" — is protected
// by a password alone. A password that, on a fresh deploy, was just read out of a
// configuration file by whoever set the box up.
//
// Deliberately a boot failure and not a warning. A warning at startup is read
// once, by one person, on the day it is installed; this is a control that has to
// hold for the life of the deployment.
if (app.Environment.IsProduction()
    && string.IsNullOrWhiteSpace(superAdminOptions.TotpSecret))
{
    throw new InvalidOperationException(
        "SuperAdmin:TotpSecret is not configured — the bootstrap super-admin "
        + "would be single-factor. Set SIMF_API_SuperAdmin__TotpSecret to a base32 "
        + "seed before starting in Production.");
}

// Refuse to start in Production when the AI prompt-hash HMAC
// secret is unconfigured; the dev-fallback key is publicly derivable.
SIMF.Infrastructure.DependencyInjection.EnsureAiPromptHashSecretConfigured(
    app.Environment.IsProduction());

// Refuse to start in Production without the PII encryption key
// (it encrypts the UserProfile national-ID / Iqama / passport / mobile columns).
SIMF.Infrastructure.DependencyInjection.EnsurePiiEncryptionConfigured(
    app.Environment.IsProduction(), app.Services);

// Refuse to start in Production without the centralized
// file-store KEK, with a clear one-line reason (the cipher otherwise fail-fasts
// deep inside the FastEndpoints/DI stack — the boot crash this guard replaces).
SIMF.Infrastructure.DependencyInjection.EnsureFileStorageEncryptionConfigured(
    app.Environment.IsProduction(), app.Services);

// Apply the migrations and seed the super-admin. Skipped under the test host,
// which prepares its own database.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    // Run the App migration BEFORE Identity. The migration pair that
    // folds UserType='Other' into 'Visitor' spans both contexts, so
    // ordering matters here. If Identity ran first and App
    // failed mid-deploy, partner accounts (AspNetUsers.UserType
    // already 'Visitor' but ProfileTypes still 'Other' + no IsVisitor
    // column yet) would be invisible to both CP queues until the App
    // migration completed. Running App first keeps the partner-side
    // ProfileType lookup valid throughout the deploy window — at
    // worst, in-flight Other users are routed by the existing
    // 'Other' UserType until Identity catches up.
    await services.GetRequiredService<SimfAppDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<SimfIdentityDbContext>().Database.MigrateAsync();
    // Warm the open-edition year before anything can write an attendee. The
    // stamp that puts the year on a new attendee record reads this cache, and a
    // cold cache stamps nothing — which the gate then admits, so the omission
    // would be silent. Reading it once here is what makes the stamp reliable
    // from the first request rather than from the first gate scan.
    await services.GetRequiredService<
        SIMF.Application.Editions.Abstractions.IEventEditionService>().GetOpenYearAsync();
    await services.GetRequiredService<IdentitySeeder>().SeedAsync();
    // The built-in rating types (App + Session) must exist in every environment
    // so the app + the end-of-session worker resolve them by code. Idempotent.
    await services.GetRequiredService<SIMF.Infrastructure.Feedback.RatingSeeder>().SeedAsync();
    // The administrative-regions lookup (the 13 official Saudi regions) is
    // required reference data the app's region picker reads, so it must exist in
    // every environment. Idempotent (keyed on Code).
    await services.GetRequiredService<SIMF.Infrastructure.Regions.RegionSeeder>().SeedAsync();
    // Default app-update config keys so the CP configuration grid is not
    // empty on a fresh DB. Every environment; idempotent. (The 2026 event CONTENT
    // this used to seed moved to the by-hand SQL lane.)
    await services.GetRequiredService<SIMF.Infrastructure.Seeding.DefaultContentSeeder>().SeedAsync();

    // In Development only, seed a few sample organisations so the
    // registration organisation picker has data before the gov Excel import.
    if (app.Environment.IsDevelopment())
    {
        await services.GetRequiredService<SIMF.Infrastructure.Organisations.OrganisationSeeder>()
            .SeedFakeAsync();

        // In Development only, apply the by-hand 2026 content-seed SQL
        // (docs/migrations/2026/*.sql: speakers, programme, news, sponsors, media
        // partners, archive, org about + the booths/delegations/FAQ/venue-map
        // gaps) so a fresh dev DB renders populated screens. Idempotent. In
        // production this SQL is run by hand; this never runs outside
        // Development.
        await services.GetRequiredService<SIMF.Infrastructure.Seeding.SqlContentSeeder>()
            .RunAsync(SIMF.Infrastructure.Seeding.SqlContentSeeder.AllFiles);
    }

    // The demo OPERATIONAL configuration (gates + the demo staff
    // operator assignment, the demo moderator's per-session grants, and the main
    // hall's seat grid). Runs LAST because it configures the content the SQL seed
    // above creates. Self-gated to Development / Seed:EnableDemoAccounts, so
    // production is a no-op; idempotent.
    await services.GetRequiredService<SIMF.Infrastructure.Seeding.DemoOperationalConfigSeeder>()
        .SeedAsync();
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

// Baseline security headers on every API response.
app.UseMiddleware<SecurityHeadersMiddleware>();

// Error handling wraps the rest of the pipeline.
app.UseMiddleware<ErrorHandlingMiddleware>();

// Apply the web CORS policy before rate-limiting/auth so the browser
// preflight (OPTIONS) is answered and the cross-origin App-API call succeeds.
// Active only when origins are configured — matching the registration.
if (webCorsEnabled)
{
    app.UseCors(devWebCorsPolicy);
}

// The X-App-Key gate on /api/v1/app/*. INERT unless the AppKey section
// configures keys, so this deploys with no behaviour change and no client
// coordination; read AppKeyOptions before populating it, because turning it on
// before a build carrying the key has shipped locks out every installed app.
// After CORS so a preflight is answered, after ErrorHandlingMiddleware so its
// refusal comes back in the standard ApiResult envelope.
app.UseMiddleware<AppKeyMiddleware>(appKeyOptions);

// Peek the request body for the email field on credential
// paths so the "auth-email" rate-limit policy can key its partition on
// it. Must run BEFORE UseRateLimiter so the key is set when the limiter
// reads it.
app.UseMiddleware<EmailRateLimitKeyMiddleware>();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Output caching runs after CORS/auth and before the endpoints, so a cache
// hit on an anonymous public read short-circuits endpoint execution. Disabled
// entirely under Testing (paired with the no-op policy above) so the suite never
// serves a stale cached read.
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseOutputCache();
}

app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "api/v1";

    // Nothing zoned on the wire: every DateTime serializes in Saudi
    // local time (+03:00, no DST). The instant is preserved; only its offset
    // representation changes. Reads accept any ISO offset (storage unaffected).
    config.Serializer.Options.Converters.Add(
        new SIMF.Api.Serialization.SaudiDateTimeOffsetJsonConverter());

    // Field-validation failures use the standard ApiResult shape.
    // Each FluentValidation rule attaches Arabic as its CustomState via the
    // .Bilingual(en, ar) extension.
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

// The OpenAPI UI is served outside production always, and in production only
// when explicitly enabled (Swagger:AllowSwagger) — and there only behind the
// Basic-auth gate, because the API is public via the reverse proxy.
if (!app.Environment.IsProduction() || swaggerOptions.AllowSwagger)
{
    if (app.Environment.IsProduction())
    {
        app.UseMiddleware<SwaggerBasicAuthMiddleware>(swaggerOptions);
    }

    app.UseSwaggerGen();
}

// The readiness endpoint.
app.MapHealthChecks("/health");

app.Run();

/// <summary>
/// Declared so the integration tests can host the API with
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
