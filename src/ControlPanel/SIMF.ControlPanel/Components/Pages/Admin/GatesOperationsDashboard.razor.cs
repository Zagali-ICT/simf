using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class GatesOperationsDashboard
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    /// <summary>The page window both tables ask for. The dashboard is a read-only
    /// overview with no pager, so it shows the most recent page and reports the true
    /// size of each set from the server's Total — which is what the stat cards and the
    /// summary lines read. Before the reports were paged, "currently inside" fetched
    /// every visitor in the venue on every refresh.</summary>
    private const int PageSize = 200;

    private IReadOnlyList<AdminCurrentlyInsideRow> _inside = Array.Empty<AdminCurrentlyInsideRow>();
    private int _insideTotal;
    private IReadOnlyList<AdminGateSummary> _gates = Array.Empty<AdminGateSummary>();
    private int _gatesTotal;
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
            var insideEnv = await JS.InvokeAsync<ApiResult<GridPage<AdminCurrentlyInsideRow>>>(
                "simfAccount.postJson",
                "/account/api/admin/gates/reports/currently-inside/list",
                new GridQuery { Top = PageSize });
            if (insideEnv is { Success: true, Data: not null })
            {
                _inside = insideEnv.Data.Items;
                _insideTotal = insideEnv.Data.Total;
            }
            else
            {
                _toast = new Toast("error",
                    insideEnv?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.GatesDashboard.LoadFailed"]);
            }

            var gatesEnv = await JS.InvokeAsync<ApiResult<GridPage<AdminGateSummary>>>(
                "simfAccount.postJson",
                "/account/api/admin/gates/list", new GridQuery { Top = PageSize });
            if (gatesEnv is { Success: true, Data: not null })
            {
                _gates = gatesEnv.Data.Items;
                _gatesTotal = gatesEnv.Data.Total;
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
