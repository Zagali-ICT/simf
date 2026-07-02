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
using SIMF.Components.Layout;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class AiServiceDetail
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string Feature { get; set; } = string.Empty;

    private record Toast(string Variant, string Message);

    private AiFeature? _feature;
    private string _active = "routing";
    private Toast? _toast;
    private List<SimfTabs.Tab> _tabs = new();

    private List<AdminAiPromptSummary> _prompts = new();
    private Guid? _activePromptId;

    private AdminAiServiceStat? _stat;
    private bool _statsLoaded;
    private bool _statsLoading;

    protected override async Task OnInitializedAsync()
    {
        _feature = Enum.TryParse<AiFeature>(Feature, ignoreCase: true, out var f)
            && Enum.IsDefined(typeof(AiFeature), f)
            ? f : null;
        _tabs = new()
        {
            new("routing", L["Admin.AiServiceDetail.Tab.Routing"]),
            new("prompts", L["Admin.AiServices.Col.Prompts"]),
            new("analytics", L["Admin.AiServiceDetail.Tab.Analytics"]),
        };
        if (_feature is not null)
        {
            await LoadPromptsAsync();
        }
    }

    private async Task OnTabChangedAsync(string key)
    {
        _active = key;
        if (key == "analytics" && !_statsLoaded)
        {
            await LoadStatsAsync();
        }
    }

    private async Task LoadPromptsAsync()
    {
        // Reuse the prompt catalogue read + filter to this feature CP-side (the
        // catalogue is tiny). The active prompt drives the Routing tab.
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminAiPromptSummary>>>(
            "simfAccount.postJson", "/account/api/admin/ai/prompts/list",
            new GridQuery { Top = 500 });
        if (env is { Success: true, Data: not null })
        {
            _prompts = env.Data.Items
                .Where(p => p.Feature == _feature)
                .OrderByDescending(p => p.IsActive)
                .ThenByDescending(p => p.Version)
                .ToList();
            _activePromptId = _prompts.FirstOrDefault(p => p.IsActive)?.Id;
        }
        else
        {
            _toast = new Toast("error",
                env?.Error?.MessageForCurrentCulture() ?? L["Admin.AiServices.LoadFailed"]);
        }
    }

    private async Task LoadStatsAsync()
    {
        _statsLoading = true;
        try
        {
            // The dashboard aggregate already breaks down per service; pick this
            // feature's row. A 403 (no AiDashboard.View) or any failure simply
            // leaves _stat null → the "no activity" state (graceful degradation).
            var env = await JS.InvokeAsync<ApiResult<AdminAiDashboard>>(
                "simfAccount.getJson", "/account/api/admin/ai/dashboard");
            if (env is { Success: true, Data: not null })
            {
                _stat = env.Data.Services.FirstOrDefault(s => s.Feature == _feature);
            }
        }
        catch
        {
            // Graceful — analytics is best-effort on this page.
        }
        finally
        {
            _statsLoaded = true;
            _statsLoading = false;
        }
    }

    private async Task OnRoutingSavedAsync()
    {
        _toast = new Toast("success", L["Admin.AiServices.Routing.Saved"]);
        await LoadPromptsAsync();
    }
}
