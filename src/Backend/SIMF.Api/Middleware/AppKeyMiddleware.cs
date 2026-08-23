using SIMF.Common;
using SIMF.Common.Options;

namespace SIMF.Api.Middleware;

/// <summary>
/// Requires a recognised <c>X-App-Key</c> on the mobile surface
/// (<c>/api/v1/app/*</c>) when, and only when, keys are configured.
/// </summary>
/// <remarks>
/// <para>
/// The header has been sent by the Flutter app and the website since the API
/// contract was written, and until now nothing read it - so it described a
/// control that did not exist. This is the read side.
/// </para>
/// <para>
/// <b>Scope is the mobile surface only.</b> Not <c>/auth/*</c>, which the
/// website and the Control Panel also drive, and not the admin surface, which is
/// gated by permissions and must never depend on a value shipped inside a client.
/// A key that is present but wrong is rejected the same as a missing one.
/// </para>
/// <para>
/// It throws <see cref="ApiException"/> rather than writing a response itself, so
/// the refusal comes back in the same <c>ApiResult</c> envelope, with the same
/// bilingual shape, as every other error. That means it must sit AFTER
/// <c>ErrorHandlingMiddleware</c> in the pipeline.
/// </para>
/// <para>
/// Read <see cref="AppKeyOptions"/> before enabling it: the gate is abuse
/// control rather than authentication, and the deploy order is not optional.
/// </para>
/// </remarks>
public sealed class AppKeyMiddleware(RequestDelegate next, AppKeyOptions options)
{
    private const string HeaderName = "X-App-Key";

    /// <summary>
    /// The mobile surface, matched with the route prefix included because that
    /// is what the path actually is by the time it reaches middleware.
    /// </summary>
    private const string MobileSurface = "/api/v1/app/";

    public async Task InvokeAsync(HttpContext context, ILogger<AppKeyMiddleware> logger)
    {
        // A CORS preflight carries no custom headers by definition - the browser
        // is ASKING whether X-App-Key may be sent - so rejecting OPTIONS here
        // would break every cross-origin call from the web app before the real
        // request was ever made. Skipping it costs nothing: the GET or POST that
        // follows the preflight is still gated.
        if (HttpMethods.IsOptions(context.Request.Method)
            || !options.IsEnabled
            || !IsMobileSurface(context.Request.Path))
        {
            await next(context);
            return;
        }

        context.Request.Headers.TryGetValue(HeaderName, out var header);

        if (!options.Accepts(header.ToString()))
        {
            // The key itself is never logged - it would put the value in the log
            // for every rejected request, which is where a leaked one gets read
            // from. The path and correlation id are enough to see a pattern.
            logger.LogWarning(
                "Rejected {Method} {Path}: missing or unrecognised {Header}.",
                context.Request.Method,
                context.Request.Path,
                HeaderName);

            throw new ApiException(
                ErrorCodes.Forbidden,
                StatusCodes.Status403Forbidden,
                "This request did not come from a recognised SIMF application.",
                "لم يصل هذا الطلب من تطبيق معتمد من الملتقى.");
        }

        await next(context);
    }

    private static bool IsMobileSurface(PathString path) =>
        path.HasValue
        && path.Value!.StartsWith(MobileSurface, StringComparison.OrdinalIgnoreCase);
}
