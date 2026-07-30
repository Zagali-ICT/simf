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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SpeakerPresentationsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private List<AdminSpeakerSummary> _speakers = new();
    private List<AdminSessionSummary> _sessions = new();

    // The full, non-paged set for the selected speaker (newest first, as the
    // endpoint returns it). The grid renders a client-side page of this.
    private List<AdminSpeakerPresentationRow> _allRows = new();

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminSpeakerPresentationRow> _page = new();

    private string _speakerId = string.Empty;
    private string _sessionId = string.Empty;
    private bool _loading;
    private bool _busy;
    private Toast? _toast;

    protected override async Task OnInitializedAsync()
    {
        await LoadSpeakersAsync();
        await LoadSessionsAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Grid.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);

    private async Task LoadSpeakersAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminSpeakerSummary>>>(
            "simfAccount.postJson", "/account/api/admin/speakers/list",
            new GridQuery { Top = 500 });
        if (env is { Success: true, Data: not null })
        {
            _speakers = env.Data.Items.ToList();
        }
    }

    private async Task LoadSessionsAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminSessionSummary>>>(
            "simfAccount.postJson", "/account/api/admin/sessions/list",
            new GridQuery { Top = 500 });
        if (env is { Success: true, Data: not null })
        {
            _sessions = env.Data.Items.Where(s => s.IsActive).ToList();
        }
    }

    private async Task SelectSpeakerAsync(string id)
    {
        _speakerId = id;
        _sessionId = string.Empty;
        _allRows = new();
        _query = new GridQuery { Top = 20 };
        _page = new();
        _toast = null;
        if (!string.IsNullOrEmpty(_speakerId))
        {
            await LoadPresentationsAsync();
        }
    }

    private void BackToSpeakers()
    {
        _speakerId = string.Empty;
        _sessionId = string.Empty;
        _allRows = new();
        _page = new();
        _toast = null;
    }

    private string SelectedSpeakerName
    {
        get
        {
            var s = _speakers.FirstOrDefault(x => x.Id.ToString() == _speakerId);
            return s is null ? string.Empty : SpeakerName(s);
        }
    }

    private static string SpeakerName(AdminSpeakerSummary s) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? (string.IsNullOrWhiteSpace(s.NameArabic) ? s.Name : s.NameArabic)
            : s.Name;

    private static string SpeakerCountry(AdminSpeakerSummary s)
    {
        var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        var name = isArabic ? s.CountryNameAr : s.CountryNameEn;
        return string.IsNullOrWhiteSpace(name) ? "—" : name;
    }

    private void OnSessionChanged(ChangeEventArgs e) =>
        _sessionId = e.Value?.ToString() ?? string.Empty;

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        // No server round-trip: the full set is already in memory. Re-page it.
        BuildPage();
        await Task.CompletedTask;
    }

    private async Task LoadPresentationsAsync()
    {
        if (string.IsNullOrEmpty(_speakerId)) return;
        _loading = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<List<AdminSpeakerPresentationRow>>>(
                "simfAccount.getJson",
                $"/account/api/admin/speakers/{_speakerId}/presentations");
            if (env is { Success: true, Data: not null })
            {
                _allRows = env.Data;
                BuildPage();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SpeakerPresentations.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    // Applies the grid's current filter, sort and page window to the in-memory
    // set. The endpoint returns the rows newest-first, which is the default
    // order when no sort is chosen.
    private void BuildPage()
    {
        IEnumerable<AdminSpeakerPresentationRow> rows = _allRows;

        if (_query.Filters.TryGetValue("fileName", out var fileFilter)
            && !string.IsNullOrWhiteSpace(fileFilter))
        {
            rows = rows.Where(r => r.FileName.Contains(
                fileFilter, StringComparison.OrdinalIgnoreCase));
        }
        if (_query.Filters.TryGetValue("sessionTitle", out var sessionFilter)
            && !string.IsNullOrWhiteSpace(sessionFilter))
        {
            rows = rows.Where(r => r.SessionTitle.Contains(
                sessionFilter, StringComparison.OrdinalIgnoreCase));
        }

        rows = (_query.Sort?.ToLowerInvariant(), _query.SortDescending) switch
        {
            ("filename", false) => rows.OrderBy(r => r.FileName),
            ("filename", true) => rows.OrderByDescending(r => r.FileName),
            ("sessiontitle", false) => rows.OrderBy(r => r.SessionTitle),
            ("sessiontitle", true) => rows.OrderByDescending(r => r.SessionTitle),
            ("sizebytes", false) => rows.OrderBy(r => r.SizeBytes),
            ("sizebytes", true) => rows.OrderByDescending(r => r.SizeBytes),
            _ => rows.OrderByDescending(r => r.CreatedAt),
        };

        var filtered = rows.ToList();
        var window = filtered.Skip(_query.Skip).Take(_query.Top).ToList();
        _page = GridPage<AdminSpeakerPresentationRow>.Of(window, filtered.Count, _query);
    }

    private async Task DownloadAsync(AdminSpeakerPresentationRow row)
    {
        // The endpoint returns the bytes with an attachment disposition, so
        // opening the URL saves the file without navigating away.
        await JS.InvokeVoidAsync("window.open",
            $"/account/api/admin/speaker-presentations/{row.Id}/file", "_blank");
    }

    // D-356 — Excel export (selected rows, or the whole filtered set for the
    // selected speaker). This grid is master-detail: the export endpoint has no
    // GridQuery list, so the chosen speaker id rides Query.Filters["speakerId"]
    // (the endpoint lists that speaker's files, then honours any selected ids).
    private async Task OnExportAsync(IReadOnlyList<AdminSpeakerPresentationRow> selected)
    {
        var query = new GridQuery
        {
            Filters = new Dictionary<string, string> { ["speakerId"] = _speakerId },
        };
        // §6.16 (F-U5-005) — a failed export used to return silently, so
        // the Export button was indistinguishable from an unwired one.
        var error = await JS.ExportXlsxAsync(
            "/account/api/admin/speaker-presentations/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.Id).ToList(),
                Query = query,
            }, L);
        if (error is not null) _toast = new Toast("error", error);
    }

    private async Task UploadAsync()
    {
        if (_busy || string.IsNullOrEmpty(_speakerId) || string.IsNullOrEmpty(_sessionId)) return;
        _busy = true;
        _toast = null;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<AdminSpeakerPresentationRow>>(
                "simfAccount.uploadFile",
                $"/account/api/admin/speakers/{_speakerId}/presentations?sessionId={_sessionId}",
                "presentation-file-input");
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.SpeakerPresentations.Uploaded"]);
                await LoadPresentationsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SpeakerPresentations.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private async Task DeleteAsync(AdminSpeakerPresentationRow row)
    {
        if (_busy) return;
        var confirmed = await JS.InvokeAsync<bool>(
            "confirm", L["Admin.SpeakerPresentations.Delete.Confirm"].Value);
        if (!confirmed) return;
        _busy = true;
        try
        {
            var env = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson",
                $"/account/api/admin/speaker-presentations/{row.Id}");
            if (env is { Success: true })
            {
                _toast = new Toast("success", L["Admin.SpeakerPresentations.Deleted"]);
                await LoadPresentationsAsync();
            }
            else
            {
                _toast = new Toast("error",
                    env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SpeakerPresentations.LoadFailed"]);
            }
        }
        finally { _busy = false; }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):0.0} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes} B";
    }
}
