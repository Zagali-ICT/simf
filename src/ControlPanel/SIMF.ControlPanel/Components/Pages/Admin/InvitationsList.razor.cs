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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class InvitationsList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "invitations";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminInvitationSummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminInvitationDetail? _target;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.Invitations.Edit.Title"]
            : L["Admin.Invitations.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.Invitations.Delete.Title"]
            : L["Admin.Invitations.Details.Title"],
        _ => string.Empty,
    };

    protected override async Task OnInitializedAsync()
    {
        _presentation = await Prefs.GetPresentationAsync(PageKey);
        await LoadAsync();
    }

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
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminInvitationSummary>>>(
                "simfAccount.postJson", "/account/api/admin/invitations/list", _query);
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Invitations.LoadFailed"]);
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

    private async Task OnEditAsync(AdminInvitationSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminInvitationSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminInvitationSummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // Edit / View / Delete all work against the full detail (mirrors
    // GET /admin/invitations/{id}) — the grid summary omits the recipient's job
    // title + the responded/updated timestamps. Returns null and surfaces a toast
    // on failure.
    private async Task<AdminInvitationDetail?> LoadDetailAsync(Guid id)
    {
        _toast = null;
        var env = await JS.InvokeAsync<ApiResult<AdminInvitationDetail>>(
            "simfAccount.getJson", $"/account/api/admin/invitations/{id}");
        if (env is { Success: true, Data: not null })
        {
            return env.Data;
        }
        _toast = new Toast("error",
            env?.Error?.MessageForCurrentCulture()
            ?? L["Admin.Invitations.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    // D-356 — Excel export (selected rows, or the current filtered set). Direct
    // download via the generic /export proxy. Export only — invitations are
    // created/edited from the CP forms, so there is no import path.
    private async Task OnExportAsync(IReadOnlyList<AdminInvitationSummary> selected)
    {
        // §6.16 (F-U5-005) — a failed export used to return silently, so
        // the Export button was indistinguishable from an unwired one.
        var error = await JS.ExportXlsxAsync(
            "/account/api/admin/invitations/export",
            new AdminGridExportRequest
            {
                Ids = selected.Select(row => row.Id).ToList(),
                Query = selected.Count == 0 ? _query : null,
            }, L);
        if (error is not null) _toast = new Toast("error", error);
    }

    private async Task OnSavedAsync(AdminInvitationDetail saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Invitations.Saved"]);
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminInvitationDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.Invitations.Deactivated"]);
        await LoadAsync();
    }

    private string RecipientLabel(AdminInvitationSummary row) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? row.RecipientArabicName
            : row.RecipientEnglishName;

    private string StateLabel(InvitationState state) => state switch
    {
        InvitationState.Confirmed => L["Admin.Invitations.State.Confirmed"],
        InvitationState.Declined => L["Admin.Invitations.State.Declined"],
        _ => L["Admin.Invitations.State.Pending"],
    };

    private static string StatePillVariant(InvitationState state) => state switch
    {
        InvitationState.Confirmed => "on",
        InvitationState.Declined => "off",
        _ => "admin",
    };
}
