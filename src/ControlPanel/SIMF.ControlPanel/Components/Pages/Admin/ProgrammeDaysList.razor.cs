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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ProgrammeDaysList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private record Toast(string Variant, string Message);

    private enum FormKind { None, AddEdit, ViewDelete }

    private const string PageKey = "programme-days";

    private GridQuery _query = new() { Top = 20 };
    private GridPage<AdminProgrammeDaySummary> _page = new();
    private bool _loading;
    private Toast? _toast;

    private CrudPresentation _presentation = CrudPresentation.Dialog;
    private FormKind _form = FormKind.None;
    private bool _isEdit;
    private bool _isDelete;
    private AdminProgrammeDayDetail? _target;

    private bool FormOpen => _form != FormKind.None;
    private bool GridHidden => FormOpen && _presentation == CrudPresentation.Page;

    private string FormTitle => _form switch
    {
        FormKind.AddEdit => _isEdit
            ? L["Admin.ProgrammeDays.Edit.Title"]
            : L["Admin.ProgrammeDays.Add.Title"],
        FormKind.ViewDelete => _isDelete
            ? L["Admin.ProgrammeDays.Delete.Title"]
            : L["Admin.ProgrammeDays.Details.Title"],
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

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminProgrammeDaySummary>>>(
                "simfAccount.postJson",
                "/account/api/admin/programme-days/list", _query);
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
            }
            else
            {
                _toast = new Toast("error",
                    envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.ProgrammeDays.LoadFailed"]);
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

    private async Task OnEditAsync(AdminProgrammeDaySummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.AddEdit;
        _isEdit = true;
        _target = detail;
    }

    private async Task OnDetailsAsync(AdminProgrammeDaySummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = false;
        _target = detail;
    }

    private async Task OnDeleteAsync(AdminProgrammeDaySummary row)
    {
        var detail = await LoadDetailAsync(row.Id);
        if (detail is null) return;
        _form = FormKind.ViewDelete;
        _isDelete = true;
        _target = detail;
    }

    // Edit / View / Delete all work against the full detail (the grid carries
    // a summary). Returns null and surfaces a toast on failure.
    private async Task<AdminProgrammeDayDetail?> LoadDetailAsync(Guid id)
    {
        _toast = null;
        var envelope = await JS.InvokeAsync<ApiResult<AdminProgrammeDayDetail>>(
            "simfAccount.getJson", $"/account/api/admin/programme-days/{id}");
        if (envelope is { Success: true, Data: not null })
        {
            return envelope.Data;
        }
        _toast = new Toast("error",
            envelope?.Error?.MessageForCurrentCulture()
            ?? L["Admin.ProgrammeDays.LoadFailed"]);
        return null;
    }

    private void CloseForm()
    {
        _form = FormKind.None;
        _target = null;
    }

    private async Task OnSavedAsync(AdminProgrammeDayDetail saved)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.ProgrammeDays.Saved"]);
        await LoadAsync();
    }

    private async Task OnDeletedAsync(AdminProgrammeDayDetail deleted)
    {
        CloseForm();
        _toast = new Toast("success", L["Admin.ProgrammeDays.Deleted"]);
        await LoadAsync();
    }

    private string FormatSummary(int skip, int taken, int total) =>
        string.Format(L["Admin.ProgrammeDays.Summary"], skip + 1, skip + taken, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Grid.Page"], current, total);
}
