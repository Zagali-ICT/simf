using System.Globalization;
using Microsoft.AspNetCore.Components;
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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class GatesOperationsDashboard
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private IReadOnlyList<AdminCurrentlyInsideRow> _inside = Array.Empty<AdminCurrentlyInsideRow>();
    private IReadOnlyList<AdminGateSummary> _gates = Array.Empty<AdminGateSummary>();
    private bool _loading;
    private Toast? _toast;

    // The simfAccount JS module is only available once the interactive Blazor
    // connection is up — running these calls in OnInitializedAsync would throw
    // on the SSR prerender pass and surface Blazor's unhandled-error banner
    // (same idiom as GateOperatorConsole).
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) { return; }
        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _toast = null;
        try
        {
            var insideEnv = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminCurrentlyInsideRow>>>(
                "simfAccount.getJson",
                "/account/api/admin/gates/reports/currently-inside");
            if (insideEnv is { Success: true, Data: not null })
            {
                _inside = insideEnv.Data;
            }
            else
            {
                _toast = new Toast("error",
                    insideEnv?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.GatesDashboard.LoadFailed"]);
            }

            var gatesEnv = await JS.InvokeAsync<ApiResult<GridPage<AdminGateSummary>>>(
                "simfAccount.postJson",
                "/account/api/admin/gates/list", new GridQuery { Top = 200 });
            if (gatesEnv is { Success: true, Data: not null })
            {
                _gates = gatesEnv.Data.Items;
            }
            else
            {
                _toast = new Toast("error",
                    gatesEnv?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.GatesDashboard.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private static string NameOf(AdminCurrentlyInsideRow row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? (string.IsNullOrWhiteSpace(row.DisplayNameArabic) ? row.DisplayName : row.DisplayNameArabic)
            : row.DisplayName;

    private static string NameOf(AdminGateSummary gate) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? (string.IsNullOrWhiteSpace(gate.NameArabic) ? gate.Name : gate.NameArabic)
            : gate.Name;
}
