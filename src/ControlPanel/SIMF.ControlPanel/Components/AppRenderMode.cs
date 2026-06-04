using Microsoft.AspNetCore.Components.Web;

namespace SIMF.ControlPanel.Components;

/// <summary>Render modes used by the Control Panel pages.</summary>
internal static class AppRenderMode
{
    /// <summary>
    /// Interactive Server with prerendering disabled — the Control Panel's
    /// global render mode (set on <c>Routes</c> and <c>HeadOutlet</c>). It is
    /// set globally because a page-level render mode does not extend to the
    /// page's layout, and the shell layout must be interactive. Prerendering is
    /// off so initialisation does not run twice on a separate DI scope.
    /// </summary>
    public static readonly InteractiveServerRenderMode InteractiveServerNoPrerender =
        new(prerender: false);
}
