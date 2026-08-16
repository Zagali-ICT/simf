using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin.ProfileTypes;

public partial class VisitorProfileTypesList
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // Every non-admin profile type lives under UserType.Visitor, so there is no
    // user-type column left to filter on; the audience-vs-partner split rides on
    // IsVisitor alone. This page is the audience queue.
    private GridQuery _query = new()
    {
        Top = 20,
        Filters = new Dictionary<string, string>
        {
            ["isVisitor"] = "true",
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
        // Always re-apply the filter — the grid drops filter keys on
        // sort/page change, but this pin is structural to the page.
        next.Filters["isVisitor"] = "true";
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
            // §6.16 (F-U5-002) — a FAILED envelope used to be substituted with an
            // empty page, so an API 500 / 403 was indistinguishable from "no rows"
            // and the admin read a working page with no data. Report it instead;
            // the page already renders a toast surface it never used on this path.
            if (envelope is { Success: true, Data: not null })
            {
                _page = envelope.Data;
            }
            else
            {
                _page = GridPage<AdminProfileTypeSummary>.Of(Array.Empty<AdminProfileTypeSummary>(), 0, _query);
                ShowToast("error", envelope?.Error?.MessageForCurrentCulture() ?? L["Admin.ProfileTypes.LoadFailed"]);
            }
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
                // 409 InUse comes back with ErrorCodes.ProfileTypeInUse; surface it explicitly.
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
