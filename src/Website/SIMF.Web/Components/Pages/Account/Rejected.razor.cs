using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;

namespace SIMF.Web.Components.Pages.Account;

// Website — rejected state-banner page (P11 — D-052). Reached when an
// authenticated visitor's account_state claim is Rejected; reads the bilingual
// rejection reason from the claims and shows it in the user's culture (with an
// EN/AR fallback). Redirects on the off-state. Markup lives in Rejected.razor.
public partial class Rejected
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private string? _reason;

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthState.GetAuthenticationStateAsync();
        var state = auth.User.FindFirst("account_state")?.Value;
        if (string.Equals(state, "Approved", StringComparison.Ordinal))
        {
            Nav.NavigateTo("/account/profile", forceLoad: true);
            return;
        }
        if (string.Equals(state, "PendingApproval", StringComparison.Ordinal))
        {
            Nav.NavigateTo("/account/pending", forceLoad: false);
            return;
        }
        var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        _reason = isArabic
            ? auth.User.FindFirst("rejection_reason_ar")?.Value
              ?? auth.User.FindFirst("rejection_reason")?.Value
            : auth.User.FindFirst("rejection_reason")?.Value
              ?? auth.User.FindFirst("rejection_reason_ar")?.Value;
    }
}
