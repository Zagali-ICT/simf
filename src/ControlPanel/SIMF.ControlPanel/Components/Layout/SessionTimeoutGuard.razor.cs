using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace SIMF.ControlPanel.Components.Layout;

public partial class SessionTimeoutGuard
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            await JS.InvokeVoidAsync("simfSessionGuard.start", new
            {
                statusUrl = "/session/status",
                signOutUrl = "/auth/sign-out",
                // Poll every 5s; warn / silently-refresh inside the last 60s of
                // the 5-min token; treat activity within the last 60s as "active".
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
        catch (JSException)
        {
            // Best-effort — if interop fails the cookie's own expiry still bounds
            // the session; we simply do not get the proactive warning.
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("simfSessionGuard.stop");
        }
        catch (JSException) { /* circuit already torn down */ }
        catch (JSDisconnectedException) { /* circuit already gone */ }
    }
}
