// Tests: SIMF.ControlPanel.Tests/ChangeAccountTypeBlockTests.cs
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ChangeAccountTypeBlock
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The account whose type is being changed.</summary>
    [Parameter, EditorRequired] public Guid AccountId { get; set; }

    /// <summary>The account's CURRENT scope: true = audience (Visitor),
    /// false = partner (Other). The target list is the opposite scope.</summary>
    [Parameter, EditorRequired] public bool IsVisitorScope { get; set; }

    /// <summary>Raised after a successful flip so the host can close/refresh.</summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    private IReadOnlyList<AdminProfileTypeSummary> _targetTypes = [];
    private AdminProfileTypeSummary? _selected;
    private bool _busy;
    private bool _done;
    private string? _error;

    private string ScopeNote => IsVisitorScope
        ? L["Admin.ChangeType.Note.VisitorToOther"]
        : L["Admin.ChangeType.Note.OtherToVisitor"];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Both audience and partner types are UserType=Visitor;
            // the picker route filters by UserType, then we narrow to the
            // OPPOSITE scope's active types (the flip targets).
            var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
                "simfAccount.getJson",
                "/account/api/admin/profile-types?userType=Visitor");
            if (envelope is { Success: true, Data: not null })
            {
                _targetTypes = envelope.Data
                    .Where(type => type.IsActive && type.IsVisitor == !IsVisitorScope)
                    .ToList();
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.ChangeType.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.ChangeType.LoadFailed"];
        }
    }

    // The scope flip used to commit on the first click.
    private bool _confirming;

    private async Task ConfirmChangeAsync()
    {
        _confirming = false;
        await ChangeAsync();
    }

    private async Task ChangeAsync()
    {
        if (_selected is null || _busy) return;
        _busy = true;
        _error = null;
        try
        {
            var body = new AdminChangeAccountTypeRequest
            {
                NewProfileTypeId = _selected.Id,
            };
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.postJson",
                $"/account/api/admin/accounts/{AccountId}/change-type", body);
            if (envelope is { Success: true })
            {
                _done = true;
                await OnChanged.InvokeAsync();
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.ChangeType.Failed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.ChangeType.Failed"];
        }
        finally { _busy = false; }
    }
}
