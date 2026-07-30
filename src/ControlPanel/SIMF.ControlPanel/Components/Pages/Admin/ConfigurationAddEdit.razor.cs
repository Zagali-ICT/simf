using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ConfigurationAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FormModel _model = new();
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Key = Initial.Key;
            _model.Value = Initial.Value;
            _model.Description = Initial.Description ?? string.Empty;
            _model.IsActive = Initial.IsActive;
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (!IsEdit)
        {
            if (string.IsNullOrWhiteSpace(_model.Key) || _model.Key.Trim().Length > 128)
            {
                _error = L["Admin.Configuration.Field.KeyInvalid"]; return;
            }
        }
        if (string.IsNullOrWhiteSpace(_model.Value) || _model.Value.Length > 1024)
        {
            _error = L["Admin.Configuration.Field.ValueInvalid"]; return;
        }
        if (_model.Description.Length > 512)
        {
            _error = L["Admin.Configuration.Field.DescriptionInvalid"]; return;
        }

        _busy = true;
        try
        {
            ApiResult<AdminSystemSettingDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminSystemSettingDetail>>(
                    "simfAccount.postJson", "/account/api/admin/system-settings",
                    new AdminCreateSystemSettingRequest
                    {
                        Key = _model.Key.Trim(),
                        Value = _model.Value.Trim(),
                        Description = NullIfBlank(_model.Description),
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminSystemSettingDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/system-settings/{Initial!.Id}",
                    new AdminUpdateSystemSettingRequest
                    {
                        Value = _model.Value.Trim(),
                        Description = NullIfBlank(_model.Description),
                        IsActive = _model.IsActive,
                    });
            }

            if (envelope is { Success: true, Data: not null })
            {
                await OnSuccess.InvokeAsync(envelope.Data);
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Configuration.Fallback"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Configuration.Fallback"];
        }
        finally { _busy = false; }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class FormModel
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
