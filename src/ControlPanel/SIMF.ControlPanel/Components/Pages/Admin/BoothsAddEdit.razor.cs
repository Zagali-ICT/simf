using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Exhibitors;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class BoothsAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();

    // D-766 — booth-officer country + coordinate inputs kept as raw strings
    // (parsed on submit), mirroring SpeakersAddEdit. Nationality is the officer's
    // own country picker.
    private string _officerCountryIdInput = string.Empty;
    private string _officerLatitudeInput = string.Empty;
    private string _officerLongitudeInput = string.Empty;

    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    // Active exhibitors + halls cached on first paint so the form's pickers are
    // ready on open (mirrors the original inline modal). Both lists are bounded,
    // so a single Top=500 round-trip each is fine.
    private List<AdminExhibitorSummary> _exhibitors = new();
    private List<AdminHallSummary> _halls = new();

    // Officer-country picker state — populated on first paint alongside the
    // exhibitor + hall lookups.
    private string[] _countryIds = Array.Empty<string>();
    private Dictionary<string, AdminCountrySummary> _countriesById = new();

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
            _model.OfficerNameArabic = Initial.OfficerNameArabic ?? string.Empty;
            _model.OfficerPhoneSecondary = Initial.OfficerPhoneSecondary ?? string.Empty;
            _model.OfficerWebsite = Initial.OfficerWebsite ?? string.Empty;
            _model.OfficerFacebookUrl = Initial.OfficerFacebookUrl ?? string.Empty;
            _model.OfficerXUrl = Initial.OfficerXUrl ?? string.Empty;
            _model.OfficerLinkedInUrl = Initial.OfficerLinkedInUrl ?? string.Empty;
            _model.OfficerInstagramUrl = Initial.OfficerInstagramUrl ?? string.Empty;
            _model.OfficerCity = Initial.OfficerCity ?? string.Empty;
            _model.OfficerCityArabic = Initial.OfficerCityArabic ?? string.Empty;
            _officerCountryIdInput = Initial.OfficerCountryId?.ToString() ?? string.Empty;
            _officerLatitudeInput = Initial.OfficerLatitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            _officerLongitudeInput = Initial.OfficerLongitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
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
        await LoadCountriesAsync();
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

    // D-766 — active countries for the officer nationality picker. Mirrors
    // SpeakersAddEdit's loader. A failure leaves the picker empty (country is
    // optional) rather than blocking the whole form.
    private async Task LoadCountriesAsync()
    {
        try
        {
            var env = await JS.InvokeAsync<ApiResult<GridPage<AdminCountrySummary>>>(
                "simfAccount.postJson", "/account/api/admin/countries/list",
                new GridQuery { Top = 500, Filters = new Dictionary<string, string> { ["isActive"] = "true" } });
            if (env is { Success: true, Data: not null })
            {
                _countriesById = env.Data.Items.ToDictionary(c => c.Id.ToString(), c => c);
                _countryIds = env.Data.Items
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => c.Name)
                    .Select(c => c.Id.ToString())
                    .ToArray();
            }
        }
        catch
        {
            // Picker stays empty; admin can still submit with no officer country.
        }
    }

    private string CountryLabel(string id)
    {
        if (!_countriesById.TryGetValue(id, out var country)) return id;
        var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        return isArabic
            ? $"{country.NameArabic} ({country.Code})"
            : $"{country.Name} ({country.Code})";
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

        // D-766 — officer country comes from the controlled picker (only valid
        // active ids or empty); the service re-validates it. Lat/long are an
        // all-or-nothing pair; the service enforces the real-world ranges.
        int? officerCountryId = null;
        if (!string.IsNullOrWhiteSpace(_officerCountryIdInput)
            && int.TryParse(_officerCountryIdInput, out var parsedCountry) && parsedCountry > 0)
        {
            officerCountryId = parsedCountry;
        }

        var coordinateError = CoordinateInput.Read(
            _officerLatitudeInput, _officerLongitudeInput, out var officerLatitude, out var officerLongitude);
        if (coordinateError is not null)
        {
            _error = L[coordinateError];
            return;
        }

        var officer = new OfficerValues(officerCountryId, officerLatitude, officerLongitude);

        _busy = true;
        try
        {
            var envelope = IsEdit
                ? await JS.InvokeAsync<ApiResult<AdminBoothDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/booths/{Initial!.Id}",
                    BuildUpdateRequest(officer))
                : await JS.InvokeAsync<ApiResult<AdminBoothDetail>>(
                    "simfAccount.postJson", "/account/api/admin/booths",
                    BuildCreateRequest(officer));

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

    private AdminCreateBoothRequest BuildCreateRequest(OfficerValues officer) => new()
    {
        Code = _model.Code.Trim(),
        Name = _model.Name.Trim(),
        NameArabic = _model.NameArabic.Trim(),
        ExhibitorId = _model.ExhibitorId,
        OfficerName = NullIfBlank(_model.OfficerName),
        OfficerPhone = NullIfBlank(_model.OfficerPhone),
        OfficerEmail = NullIfBlank(_model.OfficerEmail),
        OfficerNameArabic = NullIfBlank(_model.OfficerNameArabic),
        OfficerPhoneSecondary = NullIfBlank(_model.OfficerPhoneSecondary),
        OfficerWebsite = NullIfBlank(_model.OfficerWebsite),
        OfficerFacebookUrl = NullIfBlank(_model.OfficerFacebookUrl),
        OfficerXUrl = NullIfBlank(_model.OfficerXUrl),
        OfficerLinkedInUrl = NullIfBlank(_model.OfficerLinkedInUrl),
        OfficerInstagramUrl = NullIfBlank(_model.OfficerInstagramUrl),
        OfficerCity = NullIfBlank(_model.OfficerCity),
        OfficerCityArabic = NullIfBlank(_model.OfficerCityArabic),
        OfficerLatitude = officer.Latitude,
        OfficerLongitude = officer.Longitude,
        OfficerCountryId = officer.CountryId,
        Sector = NullIfBlank(_model.Sector),
        SectorArabic = NullIfBlank(_model.SectorArabic),
        Description = NullIfBlank(_model.Description),
        DescriptionArabic = NullIfBlank(_model.DescriptionArabic),
        HallId = _model.HallId,
        MapX = _model.MapX,
        MapY = _model.MapY,
    };

    private AdminUpdateBoothRequest BuildUpdateRequest(OfficerValues officer) => new()
    {
        Code = _model.Code.Trim(),
        Name = _model.Name.Trim(),
        NameArabic = _model.NameArabic.Trim(),
        ExhibitorId = _model.ExhibitorId,
        OfficerName = NullIfBlank(_model.OfficerName),
        OfficerPhone = NullIfBlank(_model.OfficerPhone),
        OfficerEmail = NullIfBlank(_model.OfficerEmail),
        OfficerNameArabic = NullIfBlank(_model.OfficerNameArabic),
        OfficerPhoneSecondary = NullIfBlank(_model.OfficerPhoneSecondary),
        OfficerWebsite = NullIfBlank(_model.OfficerWebsite),
        OfficerFacebookUrl = NullIfBlank(_model.OfficerFacebookUrl),
        OfficerXUrl = NullIfBlank(_model.OfficerXUrl),
        OfficerLinkedInUrl = NullIfBlank(_model.OfficerLinkedInUrl),
        OfficerInstagramUrl = NullIfBlank(_model.OfficerInstagramUrl),
        OfficerCity = NullIfBlank(_model.OfficerCity),
        OfficerCityArabic = NullIfBlank(_model.OfficerCityArabic),
        OfficerLatitude = officer.Latitude,
        OfficerLongitude = officer.Longitude,
        OfficerCountryId = officer.CountryId,
        Sector = NullIfBlank(_model.Sector),
        SectorArabic = NullIfBlank(_model.SectorArabic),
        Description = NullIfBlank(_model.Description),
        DescriptionArabic = NullIfBlank(_model.DescriptionArabic),
        HallId = _model.HallId,
        MapX = _model.MapX,
        MapY = _model.MapY,
        IsActive = _model.IsActive,
    };

    /// <summary>The booth officer's parsed contact values — the only submit inputs
    /// that are not read straight off <see cref="_model"/>.</summary>
    private sealed record OfficerValues(int? CountryId, double? Latitude, double? Longitude);

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
        public string OfficerNameArabic { get; set; } = string.Empty;
        public string OfficerPhoneSecondary { get; set; } = string.Empty;
        public string OfficerWebsite { get; set; } = string.Empty;
        public string OfficerFacebookUrl { get; set; } = string.Empty;
        public string OfficerXUrl { get; set; } = string.Empty;
        public string OfficerLinkedInUrl { get; set; } = string.Empty;
        public string OfficerInstagramUrl { get; set; } = string.Empty;
        public string OfficerCity { get; set; } = string.Empty;
        public string OfficerCityArabic { get; set; } = string.Empty;
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
