using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

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
            _model.PageColor = AdminFormDefaults.PageColor;
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
            var result = await SendAsync(
                JS,
                "/account/api/admin/themes",
                $"/account/api/admin/themes/{Initial?.Id}",
                new AdminCreateThemeRequest
                {
                    Code = _model.Code.Trim().ToUpperInvariant(),
                    Name = _model.Name.Trim(),
                    NameArabic = _model.NameArabic.Trim(),
                    Description = NullIfBlank(_model.Description),
                    DescriptionArabic = NullIfBlank(_model.DescriptionArabic),
                    DisplayOrder = order,
                    PageColor = _model.PageColor.Trim(),
                },
                new AdminUpdateThemeRequest
                {
                    Code = _model.Code.Trim().ToUpperInvariant(),
                    Name = _model.Name.Trim(),
                    NameArabic = _model.NameArabic.Trim(),
                    Description = NullIfBlank(_model.Description),
                    DescriptionArabic = NullIfBlank(_model.DescriptionArabic),
                    DisplayOrder = order,
                    PageColor = _model.PageColor.Trim(),
                    IsActive = _model.IsActive,
                });

            if (!result.Succeeded)
            {
                _error = result.ServerMessage ?? L["Admin.Themes.Fallback"];
            }
        }
        finally { _busy = false; }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
