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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ThemesAddEdit
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
            _model.Code = Initial.Code;
            _model.Name = Initial.Name;
            _model.NameArabic = Initial.NameArabic;
            _model.Description = Initial.Description ?? string.Empty;
            _model.DescriptionArabic = Initial.DescriptionArabic ?? string.Empty;
            _model.PageColor = Initial.PageColor;
            _model.IsActive = Initial.IsActive;
            _displayOrderInput = Initial.DisplayOrder.ToString();
        }
        else
        {
            _model.PageColor = "#244A77";
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Code) || _model.Code.Length is < 2 or > 16)
        {
            _error = L["Admin.Themes.Field.CodeInvalid"]; return;
        }
        if (string.IsNullOrWhiteSpace(_model.Name) || _model.Name.Length > 128)
        {
            _error = L["Admin.Themes.Field.NameInvalid"]; return;
        }
        if (string.IsNullOrWhiteSpace(_model.NameArabic) || _model.NameArabic.Length > 128)
        {
            _error = L["Admin.Themes.Field.NameArabicInvalid"]; return;
        }
        if (!int.TryParse(_displayOrderInput, out var order) || order < 0)
        {
            _error = L["Admin.Themes.Field.DisplayOrderInvalid"]; return;
        }
        if (string.IsNullOrWhiteSpace(_model.PageColor))
        {
            _error = L["Admin.Themes.Field.PageColorInvalid"]; return;
        }

        _busy = true;
        try
        {
            ApiResult<AdminThemeDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminThemeDetail>>(
                    "simfAccount.postJson", "/account/api/admin/themes",
                    new AdminCreateThemeRequest
                    {
                        Code = _model.Code.Trim().ToUpperInvariant(),
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        Description = string.IsNullOrWhiteSpace(_model.Description) ? null : _model.Description.Trim(),
                        DescriptionArabic = string.IsNullOrWhiteSpace(_model.DescriptionArabic) ? null : _model.DescriptionArabic.Trim(),
                        DisplayOrder = order,
                        PageColor = _model.PageColor.Trim(),
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminThemeDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/themes/{Initial!.Id}",
                    new AdminUpdateThemeRequest
                    {
                        Code = _model.Code.Trim().ToUpperInvariant(),
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        Description = string.IsNullOrWhiteSpace(_model.Description) ? null : _model.Description.Trim(),
                        DescriptionArabic = string.IsNullOrWhiteSpace(_model.DescriptionArabic) ? null : _model.DescriptionArabic.Trim(),
                        DisplayOrder = order,
                        PageColor = _model.PageColor.Trim(),
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
                    ?? L["Admin.Themes.Fallback"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Themes.Fallback"];
        }
        finally { _busy = false; }
    }

    private sealed class Model
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionArabic { get; set; } = string.Empty;
        public string PageColor { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
