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
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Exhibitors;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class BoothsAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    // Active exhibitors + halls cached on first paint so the form's pickers are
    // ready on open (mirrors the original inline modal). Both lists are bounded,
    // so a single Top=500 round-trip each is fine.
    private List<AdminExhibitorSummary> _exhibitors = new();
    private List<AdminHallSummary> _halls = new();

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Code = Initial.Code;
            _model.Name = Initial.Name;
            _model.NameArabic = Initial.NameArabic;
            _model.ExhibitorId = Initial.ExhibitorId;
            _model.OfficerName = Initial.OfficerName ?? string.Empty;
            _model.OfficerPhone = Initial.OfficerPhone ?? string.Empty;
            _model.OfficerEmail = Initial.OfficerEmail ?? string.Empty;
            _model.ContactId = Initial.ContactId;
            _model.Sector = Initial.Sector ?? string.Empty;
            _model.SectorArabic = Initial.SectorArabic ?? string.Empty;
            _model.Description = Initial.Description ?? string.Empty;
            _model.DescriptionArabic = Initial.DescriptionArabic ?? string.Empty;
            _model.HallId = Initial.HallId;
            _model.MapX = Initial.MapX;
            _model.MapY = Initial.MapY;
            _model.IsActive = Initial.IsActive;
        }
        _editContext = new EditContext(_model);
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadExhibitorsAsync();
        await LoadHallsAsync();
    }

    private async Task LoadExhibitorsAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminExhibitorSummary>>>(
            "simfAccount.postJson", "/account/api/admin/exhibitors/list",
            new GridQuery { Top = 500 });
        if (env is { Success: true, Data: not null })
        {
            // Only active exhibitors can staff a booth — the server enforces
            // the same rule.
            _exhibitors = env.Data.Items
                .Where(c => c.IsActive)
                .OrderBy(c => c.NameEn).ToList();
        }
        else
        {
            _error = env?.Error?.MessageForCurrentCulture()
                ?? L["Admin.Booths.ExhibitorsLoadFailed"];
        }
    }

    private async Task LoadHallsAsync()
    {
        var env = await JS.InvokeAsync<ApiResult<GridPage<AdminHallSummary>>>(
            "simfAccount.postJson", "/account/api/admin/halls/list",
            new GridQuery { Top = 500 });
        if (env is { Success: true, Data: not null })
        {
            _halls = env.Data.Items.Where(h => h.IsActive)
                .OrderBy(h => h.Name).ToList();
        }
        else
        {
            _error = env?.Error?.MessageForCurrentCulture()
                ?? L["Admin.Booths.HallsLoadFailed"];
        }
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        // Required text guards (server-side FluentValidation is the source of
        // truth; this stops an obviously-empty submit round-trip).
        if (string.IsNullOrWhiteSpace(_model.Code)
            || string.IsNullOrWhiteSpace(_model.Name)
            || string.IsNullOrWhiteSpace(_model.NameArabic))
        {
            _error = L["Admin.Booths.Required"]; return;
        }

        _busy = true;
        try
        {
            ApiResult<AdminBoothDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminBoothDetail>>(
                    "simfAccount.postJson", "/account/api/admin/booths",
                    new AdminCreateBoothRequest
                    {
                        Code = _model.Code.Trim(),
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        ExhibitorId = _model.ExhibitorId,
                        OfficerName = NullIfBlank(_model.OfficerName),
                        OfficerPhone = NullIfBlank(_model.OfficerPhone),
                        OfficerEmail = NullIfBlank(_model.OfficerEmail),
                        ContactId = _model.ContactId,
                        Sector = NullIfBlank(_model.Sector),
                        SectorArabic = NullIfBlank(_model.SectorArabic),
                        Description = NullIfBlank(_model.Description),
                        DescriptionArabic = NullIfBlank(_model.DescriptionArabic),
                        HallId = _model.HallId,
                        MapX = _model.MapX,
                        MapY = _model.MapY,
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminBoothDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/booths/{Initial!.Id}",
                    new AdminUpdateBoothRequest
                    {
                        Code = _model.Code.Trim(),
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        ExhibitorId = _model.ExhibitorId,
                        OfficerName = NullIfBlank(_model.OfficerName),
                        OfficerPhone = NullIfBlank(_model.OfficerPhone),
                        OfficerEmail = NullIfBlank(_model.OfficerEmail),
                        ContactId = _model.ContactId,
                        Sector = NullIfBlank(_model.Sector),
                        SectorArabic = NullIfBlank(_model.SectorArabic),
                        Description = NullIfBlank(_model.Description),
                        DescriptionArabic = NullIfBlank(_model.DescriptionArabic),
                        HallId = _model.HallId,
                        MapX = _model.MapX,
                        MapY = _model.MapY,
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
                    ?? L["Admin.Booths.Fallback"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Booths.Fallback"];
        }
        finally { _busy = false; }
    }

    private void OnExhibitorIdChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        _model.ExhibitorId = string.IsNullOrWhiteSpace(raw) ? null
            : (Guid.TryParse(raw, out var g) ? g : (Guid?)null);
    }

    private void OnHallIdChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        _model.HallId = string.IsNullOrWhiteSpace(raw) ? null
            : (Guid.TryParse(raw, out var g) ? g : (Guid?)null);
    }

    private void OnDescriptionEnChanged(ChangeEventArgs e) =>
        _model.Description = e.Value?.ToString() ?? string.Empty;

    private void OnDescriptionArChanged(ChangeEventArgs e) =>
        _model.DescriptionArabic = e.Value?.ToString() ?? string.Empty;

    private void OnMapXChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        _model.MapX = string.IsNullOrWhiteSpace(raw) ? null
            : (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
                ? n : (double?)null);
    }

    private void OnMapYChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        _model.MapY = string.IsNullOrWhiteSpace(raw) ? null
            : (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
                ? n : (double?)null);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class Model
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public Guid? ExhibitorId { get; set; }
        public string OfficerName { get; set; } = string.Empty;
        public string OfficerPhone { get; set; } = string.Empty;
        public string OfficerEmail { get; set; } = string.Empty;
        public Guid? ContactId { get; set; }
        public string Sector { get; set; } = string.Empty;
        public string SectorArabic { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DescriptionArabic { get; set; } = string.Empty;
        public Guid? HallId { get; set; }
        public double? MapX { get; set; }
        public double? MapY { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
