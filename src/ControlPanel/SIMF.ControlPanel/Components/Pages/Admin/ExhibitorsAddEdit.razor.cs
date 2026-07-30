using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Exhibitors;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class ExhibitorsAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ExhibitorTier value/name pairs for the (optional) tier dropdown. Aligned
    // with SIMF.Common.Enums.ExhibitorTier.
    private sealed record TierOption(int Value, string Name);

    private static readonly IReadOnlyList<TierOption> _tiers =
    [
        new((int)ExhibitorTier.Premium, "Premium"),
        new((int)ExhibitorTier.Gold, "Gold"),
        new((int)ExhibitorTier.Silver, "Silver"),
        new((int)ExhibitorTier.Bronze, "Bronze"),
    ];

    private readonly FormModel _form = new();
    private string _countryIdInput = string.Empty;
    private string _latitudeInput = string.Empty;
    private string _longitudeInput = string.Empty;
    private bool _busy;
    private string? _error;

    // Country picker state — populated lazily on first render.
    private string[] _countryIds = Array.Empty<string>();
    private Dictionary<string, AdminCountrySummary> _countriesById = new();

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _form.NameEn = Initial.NameEn;
            _form.NameAr = Initial.NameAr;
            _form.ContactEmail = Initial.ContactEmail ?? string.Empty;
            _form.ContactPhone = Initial.ContactPhone ?? string.Empty;
            _form.Website = Initial.Website ?? string.Empty;
            _form.Tier = Initial.Tier is null ? null : (int)Initial.Tier.Value;
            _countryIdInput = Initial.CountryId?.ToString() ?? string.Empty;
            _form.PhoneSecondary = Initial.PhoneSecondary ?? string.Empty;
            _form.FacebookUrl = Initial.FacebookUrl ?? string.Empty;
            _form.XUrl = Initial.XUrl ?? string.Empty;
            _form.LinkedInUrl = Initial.LinkedInUrl ?? string.Empty;
            _form.InstagramUrl = Initial.InstagramUrl ?? string.Empty;
            _form.City = Initial.City ?? string.Empty;
            _form.CityArabic = Initial.CityArabic ?? string.Empty;
            _latitudeInput = Initial.Latitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            _longitudeInput = Initial.Longitude?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            _form.IsActive = Initial.IsActive;
        }
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

    private async Task SaveAsync()
    {
        if (_busy) return;

        // Required-field client guards (the server validator is authoritative;
        // these stop a no-op round-trip and give an immediate message).
        if (string.IsNullOrWhiteSpace(_form.NameEn)
            || string.IsNullOrWhiteSpace(_form.NameAr))
        {
            _error = L["Admin.Exhibitors.NameRequired"];
            return;
        }

        // The picker only ever yields valid country ids; parse defensively and
        // fall back to "no country" if it somehow does not.
        int? countryId = null;
        if (!string.IsNullOrWhiteSpace(_countryIdInput)
            && int.TryParse(_countryIdInput, out var parsedCountry) && parsedCountry > 0)
        {
            countryId = parsedCountry;
        }

        var coordinateError = CoordinateInput.Read(
            _latitudeInput, _longitudeInput, out var latitude, out var longitude);
        if (coordinateError is not null)
        {
            _error = L[coordinateError];
            return;
        }

        _busy = true;
        _error = null;
        try
        {
            ApiResult<AdminExhibitorDetail>? env;
            if (!IsEdit)
            {
                env = await JS.InvokeAsync<ApiResult<AdminExhibitorDetail>>(
                    "simfAccount.postJson",
                    "/account/api/admin/exhibitors",
                    new CreateExhibitorRequest
                    {
                        NameEn = _form.NameEn,
                        NameAr = _form.NameAr,
                        ContactEmail = NullIfBlank(_form.ContactEmail),
                        ContactPhone = NullIfBlank(_form.ContactPhone),
                        Website = NullIfBlank(_form.Website),
                        Tier = _form.Tier is null ? null : (ExhibitorTier)_form.Tier.Value,
                        CountryId = countryId,
                        PhoneSecondary = NullIfBlank(_form.PhoneSecondary),
                        FacebookUrl = NullIfBlank(_form.FacebookUrl),
                        XUrl = NullIfBlank(_form.XUrl),
                        LinkedInUrl = NullIfBlank(_form.LinkedInUrl),
                        InstagramUrl = NullIfBlank(_form.InstagramUrl),
                        City = NullIfBlank(_form.City),
                        CityArabic = NullIfBlank(_form.CityArabic),
                        Latitude = latitude,
                        Longitude = longitude,
                    });
            }
            else
            {
                env = await JS.InvokeAsync<ApiResult<AdminExhibitorDetail>>(
                    "simfAccount.putJson",
                    $"/account/api/admin/exhibitors/{Initial!.Id}",
                    new UpdateExhibitorRequest
                    {
                        NameEn = _form.NameEn,
                        NameAr = _form.NameAr,
                        ContactEmail = NullIfBlank(_form.ContactEmail),
                        ContactPhone = NullIfBlank(_form.ContactPhone),
                        Website = NullIfBlank(_form.Website),
                        Tier = _form.Tier is null ? null : (ExhibitorTier)_form.Tier.Value,
                        CountryId = countryId,
                        PhoneSecondary = NullIfBlank(_form.PhoneSecondary),
                        FacebookUrl = NullIfBlank(_form.FacebookUrl),
                        XUrl = NullIfBlank(_form.XUrl),
                        LinkedInUrl = NullIfBlank(_form.LinkedInUrl),
                        InstagramUrl = NullIfBlank(_form.InstagramUrl),
                        City = NullIfBlank(_form.City),
                        CityArabic = NullIfBlank(_form.CityArabic),
                        Latitude = latitude,
                        Longitude = longitude,
                        IsActive = _form.IsActive,
                    });
            }

            if (env is { Success: true, Data: not null })
            {
                await OnSuccess.InvokeAsync(env.Data);
            }
            else
            {
                _error = env?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Exhibitors.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Exhibitors.LoadFailed"];
        }
        finally { _busy = false; }
    }

    private void OnTierChanged(ChangeEventArgs e)
    {
        _form.Tier = int.TryParse(e.Value?.ToString(), out var n) ? n : null;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed class FormModel
    {
        public string NameEn { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public int? Tier { get; set; }
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
