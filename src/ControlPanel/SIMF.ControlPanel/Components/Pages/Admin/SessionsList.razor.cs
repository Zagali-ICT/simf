using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SessionsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "sessions";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminSessionSummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminSessionDetail? _target;
    private CrudGridExcel? _excel;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.Sessions.Edit.Title"]
            : L["Admin.Sessions.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.Sessions.Delete.Title"]
            : L["Admin.Sessions.Details.Title"],
        _ => string.Empty,
    };

    /// <summary>Deep-link from the Speakers grid's "Sessions" action —
    /// <c>/admin/sessions?speakerId={id}</c> pre-filters the list to that
    /// speaker's sessions.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "speakerId")]
    public Guid? SpeakerId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _presentation = await Prefs.GetPresentationAsync(PageKey);
        if (SpeakerId.HasValue)
        {
            _query.Filters["speakerId"] = SpeakerId.Value.ToString();
        }
        await LoadAsync();
    }

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        _query = next;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminSessionSummary>>>(
                "simfAccount.postJson", "/account/api/admin/sessions/list", _query);
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Sessions.LoadFailed"]);
            }
        }
        finally { _loading = false; }
    }

    private Task OnAddAsync()
    {
        _toast = null;
        _form = FormKind.AddEdit;
        _isEdit = false;
        _target = null;
        return Task.CompletedTask;
    }

    private async Task OnEditAsync(AdminSessionSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminSessionSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminSessionSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // Edit / View / Delete all work against the full detail — the grid summary
    // omits the speakers/themes/recording/live URLs, so editing from a
    // summary-only form would wipe them. Returns null and surfaces a toast on
    // failure.
    private async Task<AdminSessionDetail?> LoadDetailAsync(Guid id)
    {
        _toast = null;
        var envelope = await JS.InvokeAsync<ApiResult<AdminSessionDetail>>(
            "simfAccount.getJson", $"/account/api/admin/sessions/{id}");
        if (envelope is { Success: true, Data: not null })
        {
            return envelope.Data;
        }
        _toast = new Toast("error",
            envelope?.Error?.MessageForCurrentCulture()
            ?? L["Admin.Sessions.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    // D-646 — the live Q&A moderation desk is per-session, so it is reached from the
    // Sessions grid rather than the nav (see CpNavigation). The desk page + the API
    // both enforce Questions.Moderate; the row action is wrapped in AuthorizedAction
    // for the same permission, so an admin without it never sees this button.
    private void OpenModeration(AdminSessionSummary row) =>
        Nav.NavigateTo($"/sessions/{row.Id}/moderate");

    // D-356 — Excel export/import wired to the reusable CrudGridExcel component.
    private Task OnExportAsync(IReadOnlyList<AdminSessionSummary> selected) =>
        _excel!.ExportAsync(selected.Select(row => row.Id).ToList(), _query);

    private Task OnImportAsync() => _excel!.TriggerImportAsync();

    private async Task OnImportedAsync()
    {
        _toast = new Toast("success", L["Grid.Import.Done"]);
        await LoadAsync();
    }

    private void OnExcelError(string message) => _toast = new Toast("error", message);

    private async Task OnSavedAsync(AdminSessionDetail saved)
    {
        var wasEdit = _isEdit;
        CloseForm();
        _toast = new Toast("success", SavedToastMessage(wasEdit, saved));
        await LoadAsync();
    }

    // A1 / A6 — a hall or start/end change cascade-releases every seat held for the
    // session. The generic "was updated" toast hid that completely, so the admin never
    // learned the room had been emptied (and admin row-blocks vanished with no trace
    // at all). When the API reports releases, spell out what was destroyed — the row
    // blocks especially, since nothing and nobody else reports those.
    private string SavedToastMessage(bool wasEdit, AdminSessionDetail saved)
    {
        var released = saved.ReleasedReservationCount + saved.ReleasedAdminBlockCount;
        if (wasEdit && released > 0)
        {
            return string.Format(
                L["Admin.Sessions.UpdatedWithReleases"],
                saved.Title, saved.ReleasedReservationCount, saved.ReleasedAdminBlockCount);
        }
        return string.Format(
            wasEdit ? L["Admin.Sessions.Updated"] : L["Admin.Sessions.Created"], saved.Title);
    }

    private async Task OnDeletedAsync(AdminSessionDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success",
            string.Format(L["Admin.Sessions.Deactivated"], deleted.Title));
        await LoadAsync();
    }

    // The resx keys are 1:1 with the enum names (Admin.Sessions.Status.<Name>),
    // so derive the key — no switch + dead fallback to keep in sync. A missing
    // key surfaces the raw key (normal resx-miss behaviour), not a silent mislabel.
    private string StatusLabel(SessionStatus status) =>
        L[$"Admin.Sessions.Status.{status}"];

    private static string StatusPillVariant(SessionStatus status) => status switch
    {
        SessionStatus.Published => "on",
        SessionStatus.Scheduled => "neutral",
        _ => "admin",
    };

    private static string HallLabel(AdminSessionSummary row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? row.HallNameArabic
            : row.HallName;

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Admin.Sessions.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Admin.Sessions.Pager.Page"], current, total);
}
