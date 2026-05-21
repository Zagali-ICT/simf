using Serilog;
using SIMF.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Structured logging through Serilog (SIMF-SAD-001 section 11).
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Readiness checks (SIMF-OPS-001 Amendment A.4).
builder.Services.AddHealthChecks();

// FastEndpoints and its OpenAPI document are wired in increment 3, alongside
// the first endpoint: FastEndpoints requires at least one endpoint to start.
// The package is already referenced by this project.

var app = builder.Build();

// Error handling is the first middleware, so it wraps the whole pipeline
// (SIMF-Sprint1 plan section 7).
app.UseMiddleware<ErrorHandlingMiddleware>();

// The readiness endpoint. Increment 1 reports liveness; the database and
// migration checks are added with the data layer (increment 2).
app.MapHealthChecks("/health");

app.Run();

/// <summary>
/// Declared so the integration tests can host the API with
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
