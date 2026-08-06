using System.Text.Json;

namespace SIMF.Api.RateLimiting;

/// <summary>
/// Reads the <c>email</c> field (or the <c>qrId</c> fallback for badge sign-in,
/// D-738) out of credential-touching request bodies (sign-in, forgot-password,
/// reset-password, badge-sign-in) and stashes it on the HttpContext as
/// <c>HttpContext.Items["RateLimitEmail"]</c>, so the <c>"auth-email"</c>
/// rate-limit policy can key its partition on the target account/badge —
/// independent of the source IP.
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
        "/api/v1/app/auth/sign-in",
        "/api/v1/app/auth/forgot-password",
        "/api/v1/app/auth/reset-password",
        "/api/v1/app/auth/badge-sign-in",
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

    // Cap the body buffer at 8 KB so an attacker posting a
    // 30 MB JSON body to /auth/sign-in (the Kestrel default
    // MaxRequestBodySize) cannot force a 30 MB MemoryStream allocation
    // per request on the pre-auth path. Real credential bodies are
    // well under 1 KB. Oversize requests skip the email-key extraction
    // and fall back to the per-IP "auth" partition.
    private const int MaxBodyBytes = 8 * 1024;

    private static async Task<string?> TryReadEmailAsync(HttpRequest request)
    {
        // Pre-buffer Content-Length guard — refuse oversize bodies before
        // any allocation. A missing Content-Length still buffers, but
        // EnableBuffering's bufferLimit caps the spill at MaxBodyBytes.
        if (request.ContentLength is { } declared && declared > MaxBodyBytes)
        {
            return null;
        }

        request.EnableBuffering(bufferThreshold: MaxBodyBytes, bufferLimit: MaxBodyBytes);
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
            // The badge sign-in body carries no `email` field, so fall back to the
            // scanned `qrId` — that lets the "auth-email" limiter partition per
            // target (same normalisation) instead of collapsing to the per-IP key.
            string? qrId = null;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }
                if (string.Equals(prop.Name, "email", StringComparison.OrdinalIgnoreCase))
                {
                    return prop.Value.GetString();
                }
                if (string.Equals(prop.Name, "qrId", StringComparison.OrdinalIgnoreCase))
                {
                    qrId = prop.Value.GetString();
                }
            }
            return qrId;
        }
        catch (OperationCanceledException)
        {
            // Client aborted — propagate cancellation rather than masking it.
            throw;
        }
        catch (JsonException)
        {
            // Malformed body is the endpoint's problem; the rate limiter
            // falls through to the per-IP partition. Narrowed from a bare
            // catch (was Security SEV-2.2).
            return null;
        }
        catch (IOException)
        {
            // EnableBuffering can throw IOException when bufferLimit is
            // exceeded mid-copy. Treat as "no email key" — per-IP still
            // applies, which is the right backstop for oversize bodies.
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
