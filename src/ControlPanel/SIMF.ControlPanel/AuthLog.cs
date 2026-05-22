namespace SIMF.ControlPanel;

/// <summary>
/// Resolves the logger for Control Panel authentication events — sign-in
/// completion, sign-out, and rejected sign-in attempts — so the cookie layer
/// is observable alongside the backend operation log.
/// </summary>
internal static class AuthLog
{
    /// <summary>The log category for Control Panel authentication events.</summary>
    public const string Category = "SIMF.ControlPanel.Auth";

    /// <summary>Gets the authentication-event logger from the request services.</summary>
    public static ILogger Of(HttpContext context) =>
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(Category);
}
