using Microsoft.AspNetCore.Components.Web;

namespace SIMF.ControlPanel.Components;

/// <summary>Render modes used by the Control Panel pages.</summary>
internal static class AppRenderMode
{
    /// <summary>
    /// Interactive Server with prerendering disabled. The authentication pages
    /// use this: prerendering would run their initialisation twice (once on a
    /// separate DI scope) and is the wrong default for a screen that depends on
    /// the per-circuit <c>SimfAuthSession</c>.
    /// </summary>
    public static readonly InteractiveServerRenderMode InteractiveServerNoPrerender =
        new(prerender: false);
}
