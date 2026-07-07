using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace SIMF.Web.Components.Layout;

// D-443 (NCA finding) — the token-driven session-expiry guard config for the
// signed-in Website (SSR). session-timeout.js reads window.__simfSessionGuardCfg
// and auto-starts: it silently refreshes the short-lived access token while the
// user is active and shows a "Stay signed in / Sign out" countdown once idle.
// The <script> that emits this config lives in SessionTimeoutGuard.razor.
public partial class SessionTimeoutGuard
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    // Default JsonSerializer options escape <, > and & to \u00xx, so the JSON is
    // safe to embed inside a <script> element.
    private string ConfigJson => JsonSerializer.Serialize(new
    {
        statusUrl = "/session/status",
        signOutUrl = "/auth/sign-out",
        // Poll every 5s; warn / silently-refresh inside the last 60s of the
        // 5-min token; treat activity within the last 60s as "active".
        pollMs = 5000,
        warnMs = 60000,
        activeWindowMs = 60000,
        rtl = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft,
        title = L["Session.Timeout.Title"].Value,
        body = L["Session.Timeout.Body"].Value,
        secondsLabel = L["Session.Timeout.Seconds"].Value,
        stayLabel = L["Session.Timeout.Stay"].Value,
        signOutLabel = L["Session.Timeout.SignOut"].Value,
    });
}
