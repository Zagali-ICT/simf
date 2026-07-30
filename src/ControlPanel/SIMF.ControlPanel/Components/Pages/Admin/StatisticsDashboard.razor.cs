using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class StatisticsDashboard
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    // Fully-qualified: this component's generated class is also named
    // StatisticsDashboard, so the bare contract name would bind to the
    // component instead of SIMF.Contracts.Statistics.StatisticsDashboard.
    private SIMF.Contracts.Statistics.StatisticsDashboard? _dashboard;
    private bool _loading;
    private Toast? _toast;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<SIMF.Contracts.Statistics.StatisticsDashboard>>(
                "simfAccount.getJson",
                "/account/api/admin/statistics");
            if (env is { Success: true, Data: not null })
            {
                _dashboard = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Statistics.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private static string Count(int value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
