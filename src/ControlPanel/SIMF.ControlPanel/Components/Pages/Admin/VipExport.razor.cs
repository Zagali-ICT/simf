using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class VipExport
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private GridQuery _query = new() { Top = 20, Sort = "tier" };
    private GridPage<VipRosterRow> _page = new();
    private bool _loading;

    private static bool IsArabic =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

    private static string PhotoUrl(Guid userId) => $"/account/api/admin/visitors/{userId}/vip-photo";

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<VipRosterRow>>>(
                "simfAccount.postJson",
                "/account/api/admin/visitors/vip/roster/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
        }
        finally { _loading = false; }
    }
}
