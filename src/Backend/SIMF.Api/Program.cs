using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SIMF.Api.Middleware;
using SIMF.Common;
using SIMF.Infrastructure;
using SIMF.Infrastructure.Identity;
using SIMF.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Structured logging through Serilog (SIMF-SAD-001 section 11).
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Database contexts, ASP.NET Core Identity, repositories, email pipeline.
builder.Services.AddInfrastructure(builder.Configuration);

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

var app = builder.Build();

// Apply the migrations and seed the super-admin. Skipped under the test host,
// which provides and prepares its own database.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    await services.GetRequiredService<SimfIdentityDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<SimfAppDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<IdentitySeeder>().SeedAsync();
}

// Error handling is the first middleware, so it wraps the whole pipeline
// (SIMF-Sprint1 plan section 7).
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "api/v1";

    // Field-validation failures are returned in the standard ApiResult shape
    // (SIMF-API-001 section 6-7).
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
