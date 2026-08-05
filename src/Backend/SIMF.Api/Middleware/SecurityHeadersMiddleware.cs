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
        // Written on the way out rather than on the way in. ErrorHandlingMiddleware
        // runs inside this one and calls Response.Clear() to write a failure
        // envelope, which drops any header already set — so headers applied before
        // next() survived on a 200 and vanished on every ApiException. OnStarting
        // fires after that clear, just before the headers flush, so an error
        // response carries the same headers a success does.
        context.Response.OnStarting(static state =>
        {
            var context = (HttpContext)state;
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
            if (context.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = "max-age=31536000";
            }

            // A3-4 / A5-13 / A6-21 (NCA App-Sec Standard) — the API serves JSON and is
            // never a document source, so lock the CSP down hard. Swagger UI (when
            // enabled, prod-gated behind Basic auth) renders HTML/JS, so it is exempt.
            if (!context.Request.Path.StartsWithSegments("/swagger"))
            {
                headers["Content-Security-Policy"] =
                    "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
            }

            // A5-12 (NCA App-Sec Standard) — declare an explicit charset on JSON
            // responses. FastEndpoints / WriteAsJsonAsync may emit a bare
            // "application/json"; append "; charset=utf-8" once the content type
            // is known.
            var contentType = context.Response.ContentType;
            if (contentType is not null
                && contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
                && !contentType.Contains("charset", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.ContentType = "application/json; charset=utf-8";
            }
            return Task.CompletedTask;
        }, context);

        await next(context);
    }
}
