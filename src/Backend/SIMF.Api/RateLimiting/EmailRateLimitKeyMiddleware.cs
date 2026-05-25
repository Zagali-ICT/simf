using System.Text.Json;

namespace SIMF.Api.RateLimiting;

/// <summary>
/// Reads the <c>email</c> field out of credential-touching request bodies
/// (sign-in, forgot-password, reset-password) and stashes it on the
/// HttpContext as <c>HttpContext.Items["RateLimitEmail"]</c>, so the
/// <c>"auth-email"</c> rate-limit policy can key its partition on the
/// target account — independent of the source IP.
///
/// <para>The middleware enables request-body buffering only on the
/// known credential paths, parses a small JSON document, and rewinds the
/// body so the endpoint's normal model binding still works. A malformed
/// body or a missing email field just leaves the key unset — the
/// per-IP "auth" policy still applies, the "auth-email" policy falls
/// through to a no-op partition. H7 — D-062.</para>
/// </summary>
public sealed class EmailRateLimitKeyMiddleware
{
    /// <summary>HttpContext.Items key the "auth-email" policy looks for.</summary>
    public const string ItemsKey = "SIMF.RateLimit.Email";

    private static readonly HashSet<string> CredentialPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/auth/sign-in",
        "/api/v1/auth/forgot-password",
        "/api/v1/auth/reset-password",
    };

    private readonly RequestDelegate _next;

    public EmailRateLimitKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (HttpMethods.IsPost(context.Request.Method)
            && CredentialPaths.Contains(path))
        {
            var email = await TryReadEmailAsync(context.Request);
            if (!string.IsNullOrWhiteSpace(email))
            {
                context.Items[ItemsKey] = email.Trim().ToLowerInvariant();
            }
        }
        await _next(context);
    }

    private static async Task<string?> TryReadEmailAsync(HttpRequest request)
    {
        request.EnableBuffering();
        try
        {
            // Read the body into a buffer rather than passing the stream
            // directly to JsonDocument.ParseAsync — that keeps the original
            // body stream's position predictable for FastEndpoints' model
            // binder downstream.
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);
            buffer.Position = 0;

            using var doc = await JsonDocument.ParseAsync(buffer);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            // Case-insensitive scan — System.Text.Json defaults to
            // PascalCase on the wire (`{"Email":"..."}`) but a future
            // serialiser change or a hand-rolled client may use lowercase.
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, "email", StringComparison.OrdinalIgnoreCase)
                    && prop.Value.ValueKind == JsonValueKind.String)
                {
                    return prop.Value.GetString();
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }
        }
    }

}
