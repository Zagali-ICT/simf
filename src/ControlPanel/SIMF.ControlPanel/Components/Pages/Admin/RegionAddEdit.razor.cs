using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Regions;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class RegionAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private string _sortOrderInput = "0";
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Code = Initial.Code;
            _model.NameArabic = Initial.NameArabic;
            _model.Name = Initial.Name ?? string.Empty;
            _model.IsActive = Initial.IsActive;
            _sortOrderInput = Initial.SortOrder.ToString();
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        // Code + Arabic name are the required fields (server-side
        // FluentValidation is the source of truth; this stops an obviously-empty
        // submit).
        if (string.IsNullOrWhiteSpace(_model.Code) || string.IsNullOrWhiteSpace(_model.NameArabic))
        {
            _error = L["Admin.Regions.Required"]; return;
        }
        if (!int.TryParse(_sortOrderInput, out var order) || order < 0)
        {
            order = 0;
        }

        _busy = true;
        try
        {
            var result = await SendAsync(
                JS,
                "/account/api/admin/regions",
                $"/account/api/admin/regions/{Initial?.Id}",
                new CreateRegionRequest
                {
                    Code = _model.Code.Trim(),
                    Name = NullIfBlank(_model.Name),
                    NameArabic = _model.NameArabic.Trim(),
                    SortOrder = order,
                },
                new UpdateRegionRequest
                {
                    Code = _model.Code.Trim(),
                    Name = NullIfBlank(_model.Name),
                    NameArabic = _model.NameArabic.Trim(),
                    SortOrder = order,
                    IsActive = _model.IsActive,
                });

            if (!result.Succeeded)
            {
                _error = result.ServerMessage ?? L["Admin.Regions.LoadFailed"];
            }
        }
        finally { _busy = false; }
    }

    // The English name is nullable on the wire — send null rather than an empty
    // string when the admin leaves it blank.
    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class Model
    {
        public string Code { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
