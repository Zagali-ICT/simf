using Microsoft.Extensions.Logging;

namespace SIMF.Web;

/// <summary>Helper that resolves the request-scoped <see cref="ILogger"/> for
/// authentication events on the Website (D-046 c, mirrors the CP's
/// <c>SIMF.ControlPanel.AuthLog</c>).</summary>
internal static class AuthLog
{
    public static ILogger Of(HttpContext context) =>
        context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SIMF.Web.Auth");
}
