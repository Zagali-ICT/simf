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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class AiInvocationsLog
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminAiInvocationRow> _page = new();
    private bool _loading;
    private bool _errorsOnly;
    private Toast? _toast;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        // The grid drops the errorOnly filter when it rebuilds the query for a
        // sort/filter/page change; re-apply the page-level toggle each time so
        // the "Errors only" state survives grid interactions.
        if (_errorsOnly)
        {
            _query.Filters["errorOnly"] = "true";
        }
        else
        {
            _query.Filters.Remove("errorOnly");
        }
        await LoadAsync();
    }

    private async Task OnErrorsOnlyChangedAsync(bool value)
    {
        _errorsOnly = value;
        if (_errorsOnly)
        {
            _query.Filters["errorOnly"] = "true";
        }
        else
        {
            _query.Filters.Remove("errorOnly");
        }
        _query.Skip = 0;
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
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminAiInvocationRow>>>(
                "simfAccount.postJson",
                "/account/api/admin/ai/invocations/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.AiInvocations.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // D-179 — SOC drill-down: the full redacted payload for one invocation,
    // reusing the existing GET /admin/ai/invocations/{id} endpoint (gated on
    // AiInvocations.View, the same permission this page requires).
    private AdminAiInvocationDetail? _detail;

    private async Task OnDetailAsync(AdminAiInvocationRow row)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<AdminAiInvocationDetail>>(
            "simfAccount.getJson", $"/account/api/admin/ai/invocations/{row.Id}");
        if (env is { Success: true, Data: not null })
        {
            _detail = env.Data;
        }
        else
        {
            _toast = new Toast("error",
                env?.Error?.MessageForCurrentCulture()
                ?? L["Admin.AiInvocations.LoadFailed"]);
        }
    }

    private void CloseDetail() => _detail = null;
}
