using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Exhibition;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class VenueMapAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    private List<AdminHallSummary> _halls = new();
    private List<AdminBoothSummary> _booths = new();

    protected override async Task OnInitializedAsync()
    {
        if (Initial is not null)
        {
            _model.Label = Initial.Label;
            _model.LabelArabic = Initial.LabelArabic;
            _model.Kind = Initial.Kind;
            _model.X = Initial.X;
            _model.Y = Initial.Y;
            _model.HallId = Initial.HallId?.ToString() ?? string.Empty;
            _model.BoothId = Initial.BoothId?.ToString() ?? string.Empty;
            _model.IsActive = Initial.IsActive;
        }
        _editContext = new EditContext(_model);
        await LoadPickersAsync();
    }

    private async Task LoadPickersAsync()
    {
        var halls = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
            "simfAccount.postJson", "/account/api/admin/halls/list", new GridQuery { Top = 500 });
        if (halls is { Success: true, Data: not null }) _halls = halls.Data.Items.ToList();

        var booths = await JS.InvokeAsync<ApiResult<GridPage<AdminBoothSummary>>>(
            "simfAccount.postJson", "/account/api/admin/booths/list", new GridQuery { Top = 500 });
        if (booths is { Success: true, Data: not null }) _booths = booths.Data.Items.ToList();
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Label) || string.IsNullOrWhiteSpace(_model.LabelArabic))
        {
            _error = L["Admin.VenueMap.Required"]; return;
        }

        _busy = true;
        try
        {
            Guid? hallId = Guid.TryParse(_model.HallId, out var hid) ? hid : null;
            Guid? boothId = Guid.TryParse(_model.BoothId, out var bid) ? bid : null;

            var result = await SendAsync(
                JS,
                "/account/api/admin/venue-map",
                $"/account/api/admin/venue-map/{Initial?.Id}",
                new AdminCreateVenueMapNodeRequest
                {
                    Label = _model.Label.Trim(),
                    LabelArabic = _model.LabelArabic.Trim(),
                    Kind = _model.Kind,
                    X = _model.X,
                    Y = _model.Y,
                    HallId = hallId,
                    BoothId = boothId,
                },
                new AdminUpdateVenueMapNodeRequest
                {
                    Label = _model.Label.Trim(),
                    LabelArabic = _model.LabelArabic.Trim(),
                    Kind = _model.Kind,
                    X = _model.X,
                    Y = _model.Y,
                    HallId = hallId,
                    BoothId = boothId,
                    IsActive = _model.IsActive,
                });

            if (!result.Succeeded)
            {
                _error = result.ServerMessage ?? L["Admin.VenueMap.Fallback"];
            }
        }
        finally { _busy = false; }
    }

    private void OnKindChanged(ChangeEventArgs e) =>
        _model.Kind = int.TryParse(e.Value?.ToString(), out var n) && Enum.IsDefined((VenueMapNodeKind)n)
            ? (VenueMapNodeKind)n
            : VenueMapNodeKind.Hall;

    private void OnXChanged(ChangeEventArgs e) =>
        _model.X = double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private void OnYChanged(ChangeEventArgs e) =>
        _model.Y = double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private sealed class Model
    {
        public string Label { get; set; } = string.Empty;
        public string LabelArabic { get; set; } = string.Empty;
        public VenueMapNodeKind Kind { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public string HallId { get; set; } = string.Empty;
        public string BoothId { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
