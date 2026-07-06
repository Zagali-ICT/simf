using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;

namespace SIMF.Web.Components.Pages.Account;

// Website — pending-approval state-banner page (P11 — D-052). Reached when an
// authenticated visitor's account_state claim is PendingApproval; reads the
// claim, redirects on the off-state (Approved/Rejected), and otherwise shows the
// account email + a sign-out button. Markup lives in PendingApproval.razor.
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
        if (string.Equals(state, "Approved", StringComparison.Ordinal))
        {
            Nav.NavigateTo("/account/profile", forceLoad: true);
            return;
        }
        if (string.Equals(state, "Rejected", StringComparison.Ordinal))
        {
            Nav.NavigateTo("/account/rejected", forceLoad: false);
            return;
        }
        _email = auth.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
    }
}
