namespace SIMF.Api.Middleware;

/// <summary>
/// M7 (security assessment 2026-06-20) — sets baseline security response
/// headers on every API response. The API host previously sent none. Values
/// are conservative for a JSON API consumed by the mobile app and the Blazor
/// Web/CP origins: the API is never framed, must not be MIME-sniffed, and
/// should not leak a referrer. HSTS is emitted only over HTTPS so a local
/// http dev session is unaffected. CORS stays governed by the named
/// allow-list policy (Program.cs), not by these headers.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000";
        }

        await next(context);
    }
}
