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

namespace SIMF.ControlPanel.Components.Pages.Admin.ProfileTypes;

public partial class OtherProfileTypesList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // D-186: this page is the partner-side queue. Server-side
    // contract collapsed UserType into Visitor; the partner/audience
    // split now lives on ProfileType.IsVisitor — we filter on that.
    private GridQuery _query = new()
    {
        Top = 20,
        Filters = new Dictionary<string, string>
        {
            ["userType"] = "Visitor",
            ["isVisitor"] = "false",
        },
    };
    private GridPage<AdminProfileTypeSummary>? _page;
    private bool _loading;
    private bool _busy;

    private bool _addOpen;
    private AdminProfileTypeSummary? _editTarget;
    private AdminProfileTypeSummary? _detailsTarget;
    private AdminProfileTypeSummary? _deleteTarget;
    private Toast? _toast;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task OnQueryChangedAsync(GridQuery next)
    {
        // Keep both filters pinned across grid-state changes.
        next.Filters["userType"] = "Visitor";
        next.Filters["isVisitor"] = "false";
        _query = next;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminProfileTypeSummary>>>(
                "simfAccount.postJson", "/account/api/admin/profile-types/list", _query);
            _page = envelope is { Success: true, Data: not null }
                ? envelope.Data
                : GridPage<AdminProfileTypeSummary>.Of(
                    Array.Empty<AdminProfileTypeSummary>(), 0, _query);
        }
        finally { _loading = false; }
    }

    private Task OnAddAsync()
    {
        _addOpen = true;
        return Task.CompletedTask;
    }

    private Task OnEditAsync(AdminProfileTypeSummary row)
    {
        _editTarget = row;
        return Task.CompletedTask;
    }

    private Task OnDetailsAsync(AdminProfileTypeSummary row)
    {
        _detailsTarget = row;
        return Task.CompletedTask;
    }

    private Task OnDeleteAsync(AdminProfileTypeSummary row)
    {
        _deleteTarget = row;
        return Task.CompletedTask;
    }

    private async Task OnSavedAsync(AdminProfileTypeSummary saved)
    {
        _addOpen = false;
        _editTarget = null;
        ShowToast("success", string.Format(L["Admin.ProfileTypes.Saved"], saved.Name));
        await LoadAsync();
    }

    private async Task ConfirmDeleteAsync()
    {
        if (_deleteTarget is null || _busy) return;
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.deleteJson",
                $"/account/api/admin/profile-types/{_deleteTarget.Id}");
            if (envelope is { Success: true })
            {
                ShowToast("success", string.Format(L["Admin.ProfileTypes.Delete.Success"], _deleteTarget.Name));
                _deleteTarget = null;
                await LoadAsync();
            }
            else
            {
                var localized = envelope?.Error?.MessageForCurrentCulture();
                ShowToast("error",
                    !string.IsNullOrWhiteSpace(localized)
                        ? localized
                        : L["Admin.ProfileTypes.Delete.InUse"]);
            }
        }
        finally { _busy = false; }
    }

    private string FormatSummary(int from, int to, int total) =>
        string.Format(L["Admin.Users.Pager.Summary"], from, to, total);

    private string FormatPage(int current, int total) =>
        string.Format(L["Admin.Users.Pager.Page"], current, total);

    private void ShowToast(string variant, string message)
    {
        _toast = new Toast(variant, message);
    }

    private sealed record Toast(string Variant, string Message);
}
