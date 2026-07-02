using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class InterestAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private string _displayOrderInput = "0";
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Name = Initial.Name;
            _model.NameArabic = Initial.NameArabic;
            _model.IsActive = Initial.IsActive;
            _displayOrderInput = Initial.DisplayOrder.ToString();
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Name) || _model.Name.Length > 128)
        {
            _error = L["Admin.Interests.Field.NameInvalid"]; return;
        }
        if (string.IsNullOrWhiteSpace(_model.NameArabic) || _model.NameArabic.Length > 128)
        {
            _error = L["Admin.Interests.Field.NameArabicInvalid"]; return;
        }
        if (!int.TryParse(_displayOrderInput, out var order) || order < 0)
        {
            _error = L["Admin.Interests.Field.DisplayOrderInvalid"]; return;
        }

        _busy = true;
        try
        {
            ApiResult<AdminInterestSummary>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminInterestSummary>>(
                    "simfAccount.postJson", "/account/api/admin/interests",
                    new AdminCreateInterestRequest
                    {
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        DisplayOrder = order,
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminInterestSummary>>(
                    "simfAccount.putJson", $"/account/api/admin/interests/{Initial!.Id}",
                    new AdminUpdateInterestRequest
                    {
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        DisplayOrder = order,
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
                    ?? L["Admin.Interests.Fallback"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Interests.Fallback"];
        }
        finally { _busy = false; }
    }

    private sealed class Model
    {
        public string Name { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
