using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;

namespace SIMF.ControlPanel.Components.Pages.Auth;

public partial class PendingApproval
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private string? _email;

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthState.GetAuthenticationStateAsync();
        var state = auth.User.FindFirst("account_state")?.Value;
        // If the cookie says "Approved", the user has been approved while
        // sitting on this page — send them to the dashboard.
        if (string.Equals(state, "Approved", StringComparison.Ordinal))
        {
            Nav.NavigateTo("/", forceLoad: true);
            return;
        }
        // If the cookie says "Rejected", they belong on the other page.
        if (string.Equals(state, "Rejected", StringComparison.Ordinal))
        {
            Nav.NavigateTo("/auth/rejected", forceLoad: false);
            return;
        }
        _email = auth.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
    }
}
