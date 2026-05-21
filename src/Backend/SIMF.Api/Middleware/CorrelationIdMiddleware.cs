using Serilog.Context;

namespace SIMF.Api.Middleware;

/// <summary>
/// Gives each request a correlation id — taken from the inbound
/// <c>X-Correlation-Id</c> header or generated — exposes it on
/// <see cref="HttpContext.TraceIdentifier"/> and the response, and pushes it
/// into the Serilog log context so every log line for the request carries it.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var header)
            && !string.IsNullOrWhiteSpace(header)
                ? header.ToString()
                : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
