using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Contracts.Organisations;
using SIMF.Contracts.UserProfile;

namespace SIMF.Web.Components.Pages.Account;

// User self-service profile (/account/profile, D-049) — assembles the D-044(c)
// Simf* primitives on the D-046(b) backend: the user fills the form, uploads
// their ID, and sees the QR their account was minted with on approval. Markup
// lives in UserProfile.razor. A non-Approved visitor is bounced to the matching
// state-banner page.
public partial class UserProfile
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;
    [Inject] private ILogger<UserProfile> Logger { get; set; } = default!;

    private UserProfileResponse? _response;
    private string _qrSvg = string.Empty;
    private string[] _countryCodes = Array.Empty<string>();
    private Dictionary<string, CountryDto> _countriesByCode = new();

    // P9 — interests picker.
    private InterestDto[] _interests = Array.Empty<InterestDto>();
    private List<InterestDto> _selectedInterests = new();

    private readonly UpsertUserProfileRequest _model = new();
    private string _nationalIdInput = string.Empty;
    private string _iqamaInput = string.Empty;
    private string _passportInput = string.Empty;

    // B3 — D-221 (الجهة): required organisation typeahead (native datalist).
    private string _orgSearch = string.Empty;
    private string? _orgError;
    private OrganisationPickerItem[] _orgResults = Array.Empty<OrganisationPickerItem>();
    private CancellationTokenSource? _orgSearchCts;

    private EditContext _editContext = default!;
    private ValidationMessageStore _messages = default!;
    private bool _saving;
    private bool _saveSuccess;
    private string? _saveError;
    private string? _loadError;

    private SimfFileUpload? _idImagePicker;
    private bool _uploadingIdImage;
    private bool _idImageSuccess;
    private string? _idImageError;

    protected override void OnInitialized()
    {
        _editContext = new EditContext(_model);
        _messages = new ValidationMessageStore(_editContext);
    }

    protected override async Task OnParametersSetAsync()
    {
        // P11 — D-052 guard: a non-Approved visitor belongs on the
        // matching state-banner page. The /auth/complete endpoint also
        // routes non-Approved users on first sign-in; this guard catches
        // any case where the cookie state has drifted (e.g. admin
        // rejected the visitor in another tab).
        var auth = await AuthState.GetAuthenticationStateAsync();
        var state = auth.User.FindFirst("account_state")?.Value;
        if (string.Equals(state, "PendingApproval", StringComparison.Ordinal))
        {
            Nav.NavigateTo("/account/pending", forceLoad: false);
        }
        else if (string.Equals(state, "Rejected", StringComparison.Ordinal))
        {
            Nav.NavigateTo("/account/rejected", forceLoad: false);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            // Parallel fetches — the page can't render without the first three;
            // organisations seed the picker (a failed fetch leaves it empty).
            var profileTask = JS.InvokeAsync<ApiResult<UserProfileResponse>>(
                "simfAccount.getJson", "/account/api/user-profile").AsTask();
            var countriesTask = JS.InvokeAsync<ApiResult<CountryListResponse>>(
                "simfAccount.getJson", "/account/api/user-profile/countries").AsTask();
            var interestsTask = JS.InvokeAsync<ApiResult<InterestListResponse>>(
                "simfAccount.getJson", "/account/api/interests").AsTask();
            var organisationsTask = JS.InvokeAsync<ApiResult<IReadOnlyList<OrganisationPickerItem>>>(
                "simfAccount.getJson", "/account/api/organisations?top=20").AsTask();
            await Task.WhenAll(profileTask, countriesTask, interestsTask, organisationsTask);

            var profileEnvelope = await profileTask;
            var countriesEnvelope = await countriesTask;
            var interestsEnvelope = await interestsTask;
            var organisationsEnvelope = await organisationsTask;

            if (profileEnvelope is null || !profileEnvelope.Success || profileEnvelope.Data is null
                || countriesEnvelope is null || !countriesEnvelope.Success || countriesEnvelope.Data is null
                || interestsEnvelope is null || !interestsEnvelope.Success || interestsEnvelope.Data is null)
            {
                _loadError = profileEnvelope?.Error?.MessageForCurrentCulture()
                    ?? countriesEnvelope?.Error?.MessageForCurrentCulture()
                    ?? interestsEnvelope?.Error?.MessageForCurrentCulture()
                    ?? L["Account.Profile.LoadFailed"];
                StateHasChanged();
                return;
            }

            _response = profileEnvelope.Data;
            _countriesByCode = countriesEnvelope.Data.Countries.ToDictionary(c => c.Code);
            _countryCodes = countriesEnvelope.Data.Countries.Select(c => c.Code).ToArray();
            _interests = interestsEnvelope.Data.Interests.ToArray();
            _selectedInterests = _interests
                .Where(interest => _response.InterestIds.Contains(interest.Id))
                .ToList();
            _model.InterestIds = _selectedInterests.Select(interest => interest.Id).ToList();

            _model.ArabicName = _response.ArabicName;
            _model.EnglishName = _response.EnglishName;
            _model.JobTitle = _response.JobTitle;
            _model.NationalityCode = _response.NationalityCode;
            _model.DateOfBirth = _response.DateOfBirth;
            _model.PlaceOfBirth = _response.PlaceOfBirth;
            _model.IsSaudi = _response.IsSaudi;
            _nationalIdInput = _response.NationalId ?? string.Empty;
            _iqamaInput = _response.IqamaNumber ?? string.Empty;
            _passportInput = _response.PassportNumber ?? string.Empty;
            _model.SaudiMobile = _response.SaudiMobile;
            _model.InternationalMobile = _response.InternationalMobile;

            // B3 — D-221: seed the organisation picker and pre-fill the saved
            // pick. The label resolves only if the org is in the first page of
            // results; the id is preserved regardless, so a returning user's
            // save is never blocked by a label that did not resolve.
            if (organisationsEnvelope is { Success: true, Data: not null })
            {
                _orgResults = organisationsEnvelope.Data.ToArray();
            }
            _model.OrganisationId = _response.OrganisationId;
            var currentOrg = _orgResults.FirstOrDefault(o => o.Id == _response.OrganisationId);
            _orgSearch = currentOrg is not null ? OrgLabel(currentOrg) : string.Empty;

            if (!string.IsNullOrEmpty(_response.QrId))
            {
                _qrSvg = BuildQrSvg(_response.QrId);
            }
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Loading the account profile page failed.");
            _loadError = L["Account.Profile.LoadFailed"];
            StateHasChanged();
        }
    }

    private async Task HandleSubmitAsync()
    {
        if (_saving) return;
        _messages.Clear();
        _saveSuccess = false;
        _saveError = null;

        // Map the conditional ID fields back into the request shape — the
        // API normalises NationalId / Iqama / Passport against IsSaudi but
        // sending the right field upfront keeps the audit-log detail clean.
        _model.NationalId = _model.IsSaudi
            ? (string.IsNullOrWhiteSpace(_nationalIdInput) ? null : _nationalIdInput.Trim())
            : null;
        _model.IqamaNumber = !_model.IsSaudi
            ? (string.IsNullOrWhiteSpace(_iqamaInput) ? null : _iqamaInput.Trim())
            : null;
        _model.PassportNumber = !_model.IsSaudi
            ? (string.IsNullOrWhiteSpace(_passportInput) ? null : _passportInput.Trim())
            : null;

        // B3 — D-221: organisation is required (the server enforces it too).
        _orgError = null;
        if (_model.OrganisationId is null)
        {
            _orgError = L["Account.Profile.OrganisationRequired"];
            return;
        }

        _saving = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<UserProfileResponse>>(
                "simfAccount.postJson", "/account/api/user-profile", _model);
            if (envelope is { Success: true, Data: not null })
            {
                _response = envelope.Data;
                _saveSuccess = true;
                if (!string.IsNullOrEmpty(envelope.Data.QrId))
                {
                    _qrSvg = BuildQrSvg(envelope.Data.QrId);
                }
            }
            else
            {
                _saveError = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Account.Profile.Fallback"];
            }
        }
        finally { _saving = false; }
    }

    private Task OnDateOfBirthChangedAsync(string value)
    {
        _model.DateOfBirth = DateOnly.TryParse(value, out var parsed) ? parsed : null;
        return Task.CompletedTask;
    }

    // B3 — D-221 (الجهة): organisation typeahead. The label carries the city so
    // datalist options stay distinct when two organisations share a name.
    private static string OrgLabel(OrganisationPickerItem o)
    {
        var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        var name = isArabic ? o.NameAr : (string.IsNullOrWhiteSpace(o.NameEn) ? o.NameAr : o.NameEn!);
        return string.IsNullOrWhiteSpace(o.City) ? name : $"{name} — {o.City}";
    }

    private async Task OnOrgInputAsync(ChangeEventArgs e)
    {
        _orgSearch = e.Value?.ToString() ?? string.Empty;
        // Debounce so fast typing doesn't fan out a request per keystroke.
        _orgSearchCts?.Cancel();
        _orgSearchCts?.Dispose();
        _orgSearchCts = new CancellationTokenSource();
        var token = _orgSearchCts.Token;
        var term = _orgSearch;
        try
        {
            await Task.Delay(250, token);
            await SearchOrganisationsAsync(term);
            StateHasChanged();
        }
        catch (TaskCanceledException) { /* superseded by a newer keystroke */ }
    }

    // The datalist commits a value on selection / blur; resolve it back to a
    // real organisation id. Free text that matches no option clears the pick,
    // so the required check below blocks the save until a real one is chosen.
    private void OnOrgCommitted(ChangeEventArgs e)
    {
        var value = e.Value?.ToString() ?? string.Empty;
        _orgSearch = value;
        var match = _orgResults.FirstOrDefault(
            o => string.Equals(OrgLabel(o), value, StringComparison.Ordinal));
        _model.OrganisationId = match?.Id;
        if (_model.OrganisationId is not null)
        {
            _orgError = null;
            // A datalist pick fires input (debounced search) AND change; the org
            // is already chosen, so cancel the pending search to skip a wasted fetch.
            _orgSearchCts?.Cancel();
        }
    }

    private async Task SearchOrganisationsAsync(string? term)
    {
        var url = "/account/api/organisations?top=20";
        if (!string.IsNullOrWhiteSpace(term))
        {
            url += $"&search={Uri.EscapeDataString(term.Trim())}";
        }
        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<OrganisationPickerItem>>>(
            "simfAccount.getJson", url);
        _orgResults = envelope is { Success: true, Data: not null }
            ? envelope.Data.ToArray()
            : Array.Empty<OrganisationPickerItem>();
    }

    private async Task UploadIdImageAsync()
    {
        if (_uploadingIdImage || _idImagePicker is null) return;
        _idImageError = null;
        _idImageSuccess = false;
        _uploadingIdImage = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.uploadFile",
                "/account/api/user-profile/id-image",
                _idImagePicker.ElementId);
            if (envelope is { Success: true })
            {
                _idImageSuccess = true;
                if (_response is not null)
                {
                    _response.HasIdImage = true;
                }
            }
            else
            {
                _idImageError = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Account.Profile.IdImageFallback"];
            }
        }
        finally { _uploadingIdImage = false; }
    }

    private async Task SignOutAsync() =>
        await JS.InvokeVoidAsync("simfAccount.signOut");

    private string CountryLabel(string code)
    {
        if (!_countriesByCode.TryGetValue(code, out var country)) return code;
        var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        return isArabic ? country.NameArabic : country.Name;
    }

    private static string InterestLabel(InterestDto interest)
    {
        var isArabic = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
        return isArabic ? interest.NameArabic : interest.Name;
    }

    private Task OnInterestsChanged(IReadOnlyList<InterestDto> selected)
    {
        _selectedInterests = selected.ToList();
        _model.InterestIds = _selectedInterests.Select(interest => interest.Id).ToList();
        return Task.CompletedTask;
    }

    private static string BuildQrSvg(string qrId)
    {
        // Server-side SVG QR via QRCoder — no client dependency, ships in
        // the SignalR diff as plain markup.
        using var generator = new QRCoder.QRCodeGenerator();
        using var data = generator.CreateQrCode(qrId, QRCoder.QRCodeGenerator.ECCLevel.Q);
        var svg = new QRCoder.SvgQRCode(data);
        return svg.GetGraphic(4);
    }

    public void Dispose()
    {
        _orgSearchCts?.Cancel();
        _orgSearchCts?.Dispose();
    }
}
