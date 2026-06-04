using Serilog.Context;

namespace SIMF.Api.Middleware;

/// <summary>
/// Gives each request a correlation id — taken from the inbound
/// <c>X-Correlation-Id</c> header when it is well-formed, otherwise generated —
/// exposes it on <see cref="HttpContext.TraceIdentifier"/> and the response, and
/// pushes it into the Serilog log context so every log line for the request
/// carries it.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var header)
            && IsWellFormed(header.ToString())
                ? header.ToString()
                : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    /// <summary>
    /// A client-supplied correlation id is trusted only when it is short and
    /// alphanumeric — this blocks log injection and a value that would overflow
    /// the audit-log column.
    /// </summary>
    private static bool IsWellFormed(string value) =>
        value.Length is > 0 and <= MaxLength
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
}
