using System.Security.Cryptography;
using System.Text;
using SIMF.Common.Options;

namespace SIMF.Api.Middleware;

/// <summary>
/// D-355 — HTTP Basic-auth gate for the OpenAPI (Swagger) UI in production.
/// The API is publicly reachable through the reverse proxy (SAD-001 §10.1), so
/// when Swagger is enabled in production (<see cref="SwaggerOptions.AllowSwagger"/>)
/// the <c>/swagger</c> surface must not be anonymously enumerable. This
/// middleware is only added to the pipeline in Production; non-production serves
/// Swagger openly for developers and testers (SIMF-API-001 §13).
/// </summary>
public sealed class SwaggerBasicAuthMiddleware
{
    private const string SwaggerPath = "/swagger";
    private const string Scheme = "Basic ";

    private readonly RequestDelegate _next;
    private readonly byte[] _username;
    private readonly byte[] _password;

    public SwaggerBasicAuthMiddleware(RequestDelegate next, SwaggerOptions options)
    {
        _next = next;
        _username = Encoding.UTF8.GetBytes(options.Username);
        _password = Encoding.UTF8.GetBytes(options.Password);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only the Swagger surface is gated; everything else flows through.
        if (!context.Request.Path.StartsWithSegments(SwaggerPath, StringComparison.OrdinalIgnoreCase)
            || IsAuthorized(context.Request))
        {
            await _next(context);
            return;
        }

        context.Response.Headers.WWWAuthenticate = "Basic realm=\"SIMF OpenAPI\", charset=\"UTF-8\"";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }

    private bool IsAuthorized(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (header.Length == 0 || !header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(header[Scheme.Length..].Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(decoded);
        var separator = text.IndexOf(':');
        if (separator < 0)
        {
            return false;
        }

        var user = Encoding.UTF8.GetBytes(text[..separator]);
        var pass = Encoding.UTF8.GetBytes(text[(separator + 1)..]);

        // Constant-time, non-short-circuit (&) so a caller cannot probe the
        // username/password length or a matching prefix via response timing.
        return CryptographicOperations.FixedTimeEquals(user, _username)
             & CryptographicOperations.FixedTimeEquals(pass, _password);
    }
}
