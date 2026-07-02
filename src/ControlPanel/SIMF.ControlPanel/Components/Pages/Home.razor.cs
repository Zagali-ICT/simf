using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Contracts.Logs;
using SIMF.Contracts.UserProfile;
using SIMF.Contracts.Gates;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Notifications;
using SIMF.Contracts.Faq;

namespace SIMF.ControlPanel.Components.Pages;

public partial class Home
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IAuthorizationService Authz { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    // Fully-qualified: the component's generated class and the contract share the
    // name StatisticsDashboard (same reason as the statistics page).
    private SIMF.Contracts.Statistics.StatisticsDashboard? _dashboard;
    private bool _canViewStats;

    protected override async Task OnInitializedAsync()
    {
        if (AuthState is null) { return; }
        var user = (await AuthState).User;
        _canViewStats = (await Authz.AuthorizeAsync(
            user, PermissionCatalog.PolicyFor(PermissionCatalog.Statistics.View))).Succeeded;
        if (_canViewStats)
        {
            await LoadStatsAsync();
        }
    }

    private async Task LoadStatsAsync()
    {
        // A failure here leaves the dashboard on the welcome panel only — the
        // stat cards are an enhancement, not the page's reason to exist.
        var env = await JS.InvokeAsync<ApiResult<SIMF.Contracts.Statistics.StatisticsDashboard>>(
            "simfAccount.getJson", "/account/api/admin/statistics");
        if (env is { Success: true, Data: not null })
        {
            _dashboard = env.Data;
        }
    }

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
