using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class VipsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminVipSummary> _page = new();
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    private readonly HashSet<Guid> _selected = new();

    private bool _notifyOpen;
    private string _msgTitle = string.Empty;
    private string _msgTitleArabic = string.Empty;
    private string _msgBody = string.Empty;
    private string _msgBodyArabic = string.Empty;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminVipSummary>>>(
                "simfAccount.postJson", "/account/api/admin/vips/list", _query);
            if (env is { Success: true, Data: not null })
            {
                _page = env.Data;
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Vips.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private void OnSelectionChanged(IReadOnlySet<string> keys)
    {
        _selected.Clear();
        foreach (var key in keys)
        {
            if (Guid.TryParse(key, out var id)) { _selected.Add(id); }
        }
    }

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    // D-356 — Excel export (selected rows, or the current filtered set). Direct
    // download via the generic /export proxy. Export only — the VIP list is a
    // derived view (no add/edit/import); the page's only action is bulk-notify.
    // The row id is the UserProfileId (the grid's row key).
    private async Task OnExportAsync(IReadOnlyList<AdminVipSummary> selected)
    {
        // §6.16 (F-U5-005) — a failed export used to return silently, so
        // the Export button was indistinguishable from an unwired one.
        var error = await JS.ExportXlsxAsync(
            "/account/api/admin/vips/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.UserProfileId).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }, L);
        if (error is not null) _toast = new Toast("error", error);
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private void OnNotifySelected()
    {
        if (_selected.Count == 0) return;
        _notifyOpen = true;
        _msgTitle = string.Empty;
        _msgTitleArabic = string.Empty;
        _msgBody = string.Empty;
        _msgBodyArabic = string.Empty;
    }

    private async Task SubmitNotifyAsync()
    {
        if (_busy || _selected.Count == 0) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminNotifyVipsResult>>(
                "simfAccount.postJson", "/account/api/admin/vips/notify",
                new AdminNotifyVipsRequest
                {
                    UserProfileIds = _selected.ToList(),
                    Title = _msgTitle,
                    TitleArabic = _msgTitleArabic,
                    Body = _msgBody,
                    BodyArabic = _msgBodyArabic,
                });
            if (env is { Success: true, Data: not null })
            {
                _notifyOpen = false;
                _toast = new Toast("success",
                    string.Format(L["Admin.Vips.Notify.Sent"],
                        env.Data.Dispatched, env.Data.EmailsEnqueued));
                _selected.Clear();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Vips.Notify.Failed"]);
            }
        }
        finally { _busy = false; }
    }

    private static string RecipientLabel(AdminVipSummary row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? row.ArabicName
            : row.EnglishName;

    private static string TypeLabel(AdminVipSummary row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? row.ProfileTypeNameArabic
            : row.ProfileTypeName;
}
