using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Configuration;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class WalkInModePage
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private bool _loading = true;
    private bool _busy;

    /// <summary>The master switch, read-only. Both modes resolve as
    /// <c>armed AND flag</c>, so when this is false the toggles below are inert
    /// and the page says so.</summary>
    private bool _armed;

    private string? _toastMessage;
    private string _toastVariant = "success";

    protected override async Task OnInitializedAsync()
    {
        _editContext = new EditContext(_model);
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<WalkInModeSettingsResponse>>(
                "simfAccount.getJson", "/account/api/admin/walk-in-mode");
            if (envelope is { Success: true, Data: not null })
            {
                Apply(envelope.Data);
            }
            else
            {
                _toastVariant = "error";
                _toastMessage = L["Admin.WalkInMode.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _toastVariant = "error";
            _toastMessage = L["Admin.WalkInMode.LoadFailed"];
        }
        finally { _loading = false; }
    }

    private async Task SaveAsync()
    {
        if (_busy) { return; }
        _busy = true;
        _toastMessage = null;
        try
        {
            // Always sends an explicit true/false, never null: this page shows two
            // checkboxes, and a checkbox has no "defer to configuration" state to
            // express. Clearing an override is available through the system-
            // settings grid, which is where a key-level edit belongs.
            var envelope = await JS.InvokeAsync<ApiResult<WalkInModeSettingsResponse>>(
                "simfAccount.postJson", "/account/api/admin/walk-in-mode",
                new AdminUpdateWalkInModeRequest
                {
                    QuickRegister = _model.QuickRegister,
                    AutoApprove = _model.AutoApprove,
                });
            if (envelope is { Success: true, Data: not null })
            {
                Apply(envelope.Data);
                _toastVariant = "success";
                _toastMessage = L["Admin.WalkInMode.Saved"];
            }
            else
            {
                _toastVariant = "error";
                _toastMessage = L["Admin.WalkInMode.SaveFailed"];
            }
        }
        catch (Exception)
        {
            _toastVariant = "error";
            _toastMessage = L["Admin.WalkInMode.SaveFailed"];
        }
        finally
        {
            _busy = false;
        }
    }

    private void Apply(WalkInModeSettingsResponse data)
    {
        _armed = data.Armed;
        _model.QuickRegister = data.QuickRegister;
        _model.AutoApprove = data.AutoApprove;
    }

    private sealed class Model
    {
        public bool QuickRegister { get; set; }

        public bool AutoApprove { get; set; }
    }
}
