using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.PublicRelations;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class MediaPartnerAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private string _displayOrderInput = "0";
    private string _countryIdInput = string.Empty;
    private string _latitudeInput = string.Empty;
    private string _longitudeInput = string.Empty;
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    // Country picker state — populated lazily on first render.
    private string[] _countryIds = Array.Empty<string>();
    private Dictionary<string, AdminCountrySummary> _countriesById = new();

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Name = Initial.Name;
            _model.NameArabic = Initial.NameArabic;
            _model.LogoRelativePath = Initial.LogoRelativePath ?? string.Empty;
            _model.Url = Initial.Url ?? string.Empty;
            _model.IsActive = Initial.IsActive;
            _displayOrderInput = Initial.DisplayOrder.ToString();
            _countryIdInput = Initial.CountryId?.ToString() ?? string.Empty;
            _model.Email = Initial.Email ?? string.Empty;
            _model.PhonePrimary = Initial.PhonePrimary ?? string.Empty;
            _model.PhoneSecondary = Initial.PhoneSecondary ?? string.Empty;
            _model.FacebookUrl = Initial.FacebookUrl ?? string.Empty;
            _model.XUrl = Initial.XUrl ?? string.Empty;
            _model.LinkedInUrl = Initial.LinkedInUrl ?? string.Empty;
            _model.InstagramUrl = Initial.InstagramUrl ?? string.Empty;
            _model.City = Initial.City ?? string.Empty;
            _model.CityArabic = Initial.CityArabic ?? string.Empty;
            _latitudeInput = Initial.Latitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            _longitudeInput = Initial.Longitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }
        _editContext = new EditContext(_model);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<GridPage<AdminCountrySummary>>>(
                "simfAccount.postJson", "/account/api/admin/countries/list",
                new GridQuery { Top = 500, Filters = new Dictionary<string, string> { ["isActive"] = "true" } });
            if (envelope is { Success: true, Data: not null })
            {
                _countriesById = envelope.Data.Items.ToDictionary(c => c.Id.ToString(), c => c);
                _countryIds = envelope.Data.Items
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => c.Name)
                    .Select(c => c.Id.ToString())
                    .ToArray();
                StateHasChanged();
            }
        }
        catch
        {
            // Picker stays empty; admin can still submit with no country.
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

        var form = ReadForm();
        if (form is null) { return; }

        _busy = true;
        try
        {
            var envelope = IsEdit
                ? await JS.InvokeAsync<ApiResult<AdminMediaPartnerDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/media-partners/{Initial!.Id}",
                    BuildUpdateRequest(form))
                : await JS.InvokeAsync<ApiResult<AdminMediaPartnerDetail>>(
                    "simfAccount.postJson", "/account/api/admin/media-partners",
                    BuildCreateRequest(form));

            if (envelope is { Success: true, Data: not null })
            {
                await OnSuccess.InvokeAsync(envelope.Data);
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.MediaPartners.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.MediaPartners.LoadFailed"];
        }
        finally { _busy = false; }
    }

    /// <summary>Validates the form and returns its parsed values, or null after
    /// setting <see cref="_error"/> to the first problem found.</summary>
    private FormValues? ReadForm()
    {
        if (string.IsNullOrWhiteSpace(_model.Name) || string.IsNullOrWhiteSpace(_model.NameArabic))
        {
            _error = L["Admin.MediaPartners.NameRequired"];
            return null;
        }

        // An unreadable display order is not worth blocking a save over — it
        // falls back to the front of the list, as it always has.
        if (!int.TryParse(_displayOrderInput, out var order) || order < 0)
        {
            order = 0;
        }

        int? countryId = null;
        if (!string.IsNullOrWhiteSpace(_countryIdInput))
        {
            if (!int.TryParse(_countryIdInput, out var parsed) || parsed <= 0)
            {
                _error = L["Admin.ContactField.CountryInvalid"];
                return null;
            }
            countryId = parsed;
        }

        var coordinateError = CoordinateInput.Read(
            _latitudeInput, _longitudeInput, out var latitude, out var longitude);
        if (coordinateError is not null)
        {
            _error = L[coordinateError];
            return null;
        }

        return new FormValues(order, countryId, latitude, longitude);
    }

    private AdminCreateMediaPartnerRequest BuildCreateRequest(FormValues form) => new(
        _model.Name.Trim(),
        _model.NameArabic.Trim(),
        NullIfBlank(_model.LogoRelativePath),
        NullIfBlank(_model.Url),
        form.DisplayOrder,
        Email: NullIfBlank(_model.Email),
        PhonePrimary: NullIfBlank(_model.PhonePrimary),
        PhoneSecondary: NullIfBlank(_model.PhoneSecondary),
        FacebookUrl: NullIfBlank(_model.FacebookUrl),
        XUrl: NullIfBlank(_model.XUrl),
        LinkedInUrl: NullIfBlank(_model.LinkedInUrl),
        InstagramUrl: NullIfBlank(_model.InstagramUrl),
        City: NullIfBlank(_model.City),
        CityArabic: NullIfBlank(_model.CityArabic),
        CountryId: form.CountryId,
        Latitude: form.Latitude,
        Longitude: form.Longitude);

    private AdminUpdateMediaPartnerRequest BuildUpdateRequest(FormValues form) => new()
    {
        Name = _model.Name.Trim(),
        NameArabic = _model.NameArabic.Trim(),
        LogoRelativePath = NullIfBlank(_model.LogoRelativePath),
        Url = NullIfBlank(_model.Url),
        DisplayOrder = form.DisplayOrder,
        IsActive = _model.IsActive,
        Email = NullIfBlank(_model.Email),
        PhonePrimary = NullIfBlank(_model.PhonePrimary),
        PhoneSecondary = NullIfBlank(_model.PhoneSecondary),
        FacebookUrl = NullIfBlank(_model.FacebookUrl),
        XUrl = NullIfBlank(_model.XUrl),
        LinkedInUrl = NullIfBlank(_model.LinkedInUrl),
        InstagramUrl = NullIfBlank(_model.InstagramUrl),
        City = NullIfBlank(_model.City),
        CityArabic = NullIfBlank(_model.CityArabic),
        CountryId = form.CountryId,
        Latitude = form.Latitude,
        Longitude = form.Longitude,
    };

    /// <summary>The form's validated, parsed values — everything the two request
    /// builders need that is not read straight off <see cref="_model"/>.</summary>
    private sealed record FormValues(
        int DisplayOrder,
        int? CountryId,
        double? Latitude,
        double? Longitude);

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class Model
    {
        public string Name { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public string LogoRelativePath { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhonePrimary { get; set; } = string.Empty;
        public string PhoneSecondary { get; set; } = string.Empty;
        public string FacebookUrl { get; set; } = string.Empty;
        public string XUrl { get; set; } = string.Empty;
        public string LinkedInUrl { get; set; } = string.Empty;
        public string InstagramUrl { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string CityArabic { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
