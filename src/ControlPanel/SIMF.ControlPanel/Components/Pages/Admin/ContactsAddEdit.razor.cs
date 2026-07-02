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
using SIMF.Contracts.Contacts;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ContactsAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly FormModel _model = new();
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    // Country picker — loaded once on first render (active rows only).
    private string[] _countryIds = Array.Empty<string>();
    private Dictionary<string, AdminCountrySummary> _countriesById = new();

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.NameAr = Initial.NameAr;
            _model.NameEn = Initial.NameEn ?? string.Empty;
            _model.LogoRelativePath = Initial.LogoRelativePath ?? string.Empty;
            _model.PhonePrimary = Initial.PhonePrimary ?? string.Empty;
            _model.PhoneSecondary = Initial.PhoneSecondary ?? string.Empty;
            _model.Email = Initial.Email ?? string.Empty;
            _model.Website = Initial.Website ?? string.Empty;
            _model.FacebookUrl = Initial.FacebookUrl ?? string.Empty;
            _model.XUrl = Initial.XUrl ?? string.Empty;
            _model.LinkedInUrl = Initial.LinkedInUrl ?? string.Empty;
            _model.InstagramUrl = Initial.InstagramUrl ?? string.Empty;
            _model.CountryId = Initial.CountryId?.ToString() ?? string.Empty;
            _model.City = Initial.City ?? string.Empty;
            _model.CityArabic = Initial.CityArabic ?? string.Empty;
            _model.Latitude = Initial.Latitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            _model.Longitude = Initial.Longitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            _model.IsActive = Initial.IsActive;
        }
        _editContext = new EditContext(_model);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
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
                StateHasChanged();
            }
        }
        catch
        {
            // Picker stays empty; admin can still save with no country.
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

        // Arabic name is the only required field; server-side validation is the
        // source of truth (this stops an obviously-empty submit).
        if (string.IsNullOrWhiteSpace(_model.NameAr))
        {
            _error = L["Admin.Contacts.Required"]; return;
        }
        // Lat/long are an all-or-nothing pair (mirrors the server ContactInvalid rule).
        if (!TryParseLatLong(out var lat, out var lng, out var pairError))
        {
            _error = pairError; return;
        }
        int? countryId = null;
        if (!string.IsNullOrWhiteSpace(_model.CountryId)
            && int.TryParse(_model.CountryId, out var parsedCountry) && parsedCountry > 0)
        {
            countryId = parsedCountry;
        }

        _busy = true;
        try
        {
            ApiResult<AdminContactDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminContactDetail>>(
                    "simfAccount.postJson", "/account/api/admin/contacts",
                    new CreateContactRequest
                    {
                        NameAr = _model.NameAr.Trim(),
                        NameEn = NullIfBlank(_model.NameEn),
                        LogoRelativePath = NullIfBlank(_model.LogoRelativePath),
                        PhonePrimary = NullIfBlank(_model.PhonePrimary),
                        PhoneSecondary = NullIfBlank(_model.PhoneSecondary),
                        Email = NullIfBlank(_model.Email),
                        Website = NullIfBlank(_model.Website),
                        FacebookUrl = NullIfBlank(_model.FacebookUrl),
                        XUrl = NullIfBlank(_model.XUrl),
                        LinkedInUrl = NullIfBlank(_model.LinkedInUrl),
                        InstagramUrl = NullIfBlank(_model.InstagramUrl),
                        City = NullIfBlank(_model.City),
                        CityArabic = NullIfBlank(_model.CityArabic),
                        Latitude = lat,
                        Longitude = lng,
                        CountryId = countryId,
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminContactDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/contacts/{Initial!.Id}",
                    new UpdateContactRequest
                    {
                        NameAr = _model.NameAr.Trim(),
                        NameEn = NullIfBlank(_model.NameEn),
                        LogoRelativePath = NullIfBlank(_model.LogoRelativePath),
                        PhonePrimary = NullIfBlank(_model.PhonePrimary),
                        PhoneSecondary = NullIfBlank(_model.PhoneSecondary),
                        Email = NullIfBlank(_model.Email),
                        Website = NullIfBlank(_model.Website),
                        FacebookUrl = NullIfBlank(_model.FacebookUrl),
                        XUrl = NullIfBlank(_model.XUrl),
                        LinkedInUrl = NullIfBlank(_model.LinkedInUrl),
                        InstagramUrl = NullIfBlank(_model.InstagramUrl),
                        City = NullIfBlank(_model.City),
                        CityArabic = NullIfBlank(_model.CityArabic),
                        Latitude = lat,
                        Longitude = lng,
                        CountryId = countryId,
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
                    ?? L["Admin.Contacts.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Contacts.LoadFailed"];
        }
        finally { _busy = false; }
    }

    // Lat/long must be supplied together (or both blank). Returns parsed doubles
    // or an error message — mirrors the server's ContactInvalid pairing rule.
    private bool TryParseLatLong(out double? lat, out double? lng, out string? error)
    {
        lat = null; lng = null; error = null;
        var hasLat = !string.IsNullOrWhiteSpace(_model.Latitude);
        var hasLng = !string.IsNullOrWhiteSpace(_model.Longitude);
        if (!hasLat && !hasLng) return true;
        if (hasLat != hasLng)
        {
            error = L["Admin.Contacts.Field.LatLongPair"];
            return false;
        }
        if (!double.TryParse(_model.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latV)
            || latV is < -90 or > 90
            || !double.TryParse(_model.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lngV)
            || lngV is < -180 or > 180)
        {
            error = L["Admin.Contacts.Field.LatLongInvalid"];
            return false;
        }
        lat = latV; lng = lngV;
        return true;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class FormModel
    {
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string LogoRelativePath { get; set; } = string.Empty;
        public string PhonePrimary { get; set; } = string.Empty;
        public string PhoneSecondary { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public string FacebookUrl { get; set; } = string.Empty;
        public string XUrl { get; set; } = string.Empty;
        public string LinkedInUrl { get; set; } = string.Empty;
        public string InstagramUrl { get; set; } = string.Empty;
        public string CountryId { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string CityArabic { get; set; } = string.Empty;
        public string Latitude { get; set; } = string.Empty;
        public string Longitude { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
