using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.ApiClient;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.Contracts.Notifications;

namespace SIMF.ControlPanel.Components.Pages.Auth;

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
            Nav.NavigateTo("/", forceLoad: true);
            return;
        }
        if (string.Equals(state, "PendingApproval", StringComparison.Ordinal))
        {
            Nav.NavigateTo("/auth/pending", forceLoad: false);
            return;
        }
        var isArabic = System.Globalization.CultureInfo.CurrentUICulture
            .TwoLetterISOLanguageName == "ar";
        _reason = isArabic
            ? auth.User.FindFirst("rejection_reason_ar")?.Value
              ?? auth.User.FindFirst("rejection_reason")?.Value
            : auth.User.FindFirst("rejection_reason")?.Value
              ?? auth.User.FindFirst("rejection_reason_ar")?.Value;
    }
}
