using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SIMF.Api.Middleware;
using SIMF.Api.RateLimiting;
using SIMF.Api.RequestContext;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Domain.Auditing;
using SIMF.Infrastructure;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Structured logging through Serilog (SIMF-SAD-001 section 11).
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Database contexts, ASP.NET Core Identity, repositories, email, the audit log.
builder.Services.AddInfrastructure(builder.Configuration);

// The audit log reads the request context; the API supplies it from HttpContext.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();

// Rate limiting for the authentication endpoints — a fixed window per client IP.
var rateLimitOptions =
    builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
    ?? new RateLimitOptions();

builder.Services.AddRateLimiter(rateLimiter =>
{
    rateLimiter.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.PermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
            }));

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

app.UseRateLimiter();

app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "api/v1";

    // Field-validation failures use the standard ApiResult shape (API-001 §6-7).
    config.Errors.ResponseBuilder = (failures, _, _) =>
        ApiResult<object>.Fail(new ApiError
        {
            Code = ErrorCodes.ValidationFailed,
            Message = "One or more fields are invalid.",
            Details = failures
                .Select(failure => new ApiErrorDetail
                {
                    Field = failure.PropertyName,
                    Message = failure.ErrorMessage,
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
