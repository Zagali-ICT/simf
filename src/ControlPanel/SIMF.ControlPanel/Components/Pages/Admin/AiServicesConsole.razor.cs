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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class AiServicesConsole
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private record Toast(string Variant, string Message);

    // One AI service (feature) with its active prompt's routing, aggregated from
    // the prompt catalogue. ActiveKey is null when the feature has prompts but
    // none active.
    private sealed record ServiceRow(
        AiFeature Feature, string DisplayName, string? ActiveKey, Guid? ActivePromptId,
        AiProvider Provider, string Model, int Version, int PromptCount);

    private List<ServiceRow> _rows = new();
    private GridQuery _query = new() { Top = 20 };
    private GridPage<ServiceRow> _page = new();
    private bool _loading;
    private Toast? _toast;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        BuildPage();
        return Task.CompletedTask;
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
            // Read the whole prompt catalogue once and group it CP-side — no new
            // endpoint. The catalogue is small (one prompt per feature, a few for
            // A/B), so Top covers it comfortably.
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminAiPromptSummary>>>(
                "simfAccount.postJson", "/account/api/admin/ai/prompts/list",
                new GridQuery { Top = 500 });
            if (env is { Success: true, Data: not null })
            {
                _rows = Aggregate(env.Data.Items);
                BuildPage();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.AiServices.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // Group prompts by feature → one ServiceRow each. The "active prompt" is the
    // first active one (the routing the service actually uses); if none is active
    // the most recent prompt supplies the display info but ActiveKey stays null.
    private static List<ServiceRow> Aggregate(IReadOnlyList<AdminAiPromptSummary> prompts)
    {
        var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        return prompts
            .GroupBy(p => p.Feature)
            .Select(g =>
            {
                var ordered = g.OrderByDescending(p => p.IsActive)
                    .ThenByDescending(p => p.Version)
                    .ToList();
                var lead = ordered[0];
                var active = ordered.FirstOrDefault(p => p.IsActive);
                var name = isArabic ? lead.DisplayNameArabic : lead.DisplayName;
                return new ServiceRow(
                    lead.Feature,
                    string.IsNullOrWhiteSpace(name) ? lead.DisplayName : name,
                    active?.Key,
                    active?.Id,
                    lead.Provider,
                    lead.Model,
                    lead.Version,
                    g.Count());
            })
            .OrderBy(r => r.Feature)
            .ToList();
    }

    // Filter / sort / page over the in-memory rows (the desk loads them all once).
    private void BuildPage()
    {
        IEnumerable<ServiceRow> q = _rows;
        if (_query.Filters.TryGetValue("service", out var f) && !string.IsNullOrWhiteSpace(f))
        {
            q = q.Where(r => r.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase)
                || r.Feature.ToString().Contains(f, StringComparison.OrdinalIgnoreCase));
        }
        q = (_query.Sort, _query.SortDescending) switch
        {
            ("provider", false) => q.OrderBy(r => r.Provider),
            ("provider", true) => q.OrderByDescending(r => r.Provider),
            ("prompts", false) => q.OrderBy(r => r.PromptCount),
            ("prompts", true) => q.OrderByDescending(r => r.PromptCount),
            ("service", true) => q.OrderByDescending(r => r.DisplayName),
            ("service", false) => q.OrderBy(r => r.DisplayName),
            _ => q,
        };
        var filtered = q.ToList();
        var items = filtered.Skip(_query.Skip).Take(_query.Top).ToList();
        _page = GridPage<ServiceRow>.Of(items, filtered.Count, _query);
    }

    // CP Phase-1/2 — set a service's provider/model from the console. The shared
    // AiRoutingEditor (hosted in the modal) loads the active prompt + PUTs the
    // routing change; this page only opens/closes it and reloads on save. The
    // action is gated by AiPrompts.Edit (the editor's PUT permission).
    private Guid? _routingPromptId;

    private void OpenRouting(Guid promptId) => _routingPromptId = promptId;

    private async Task OnRoutingSavedAsync()
    {
        _routingPromptId = null;
        _toast = new Toast("success", L["Admin.AiServices.Routing.Saved"]);
        await LoadAsync();
    }

    private void CloseRouting() => _routingPromptId = null;
}
