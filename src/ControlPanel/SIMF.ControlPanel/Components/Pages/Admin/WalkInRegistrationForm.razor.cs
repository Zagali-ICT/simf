using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Components.Forms;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Configuration;
using SIMF.Contracts.Organisations;
using SIMF.Contracts.Regions;
using SIMF.Contracts.UserProfile;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class WalkInRegistrationForm : IDisposable
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private ValidationMessageStore _messages = default!;
    private bool _busy;
    private string? _error;
    private AdminProfileTypeSummary[] _profileTypes = Array.Empty<AdminProfileTypeSummary>();
    private CountryDto[] _countries = Array.Empty<CountryDto>();
    private InterestDto[] _interests = Array.Empty<InterestDto>();
    // Birth-region options. Loaded from the DB-backed regions lookup
    // (/admin/regions/list, active only) in OnInitializedAsync; seeded up-front
    // from the offline SaudiRegions constant so the picker is never empty and so
    // it stays populated if the fetch fails or returns nothing.
    private RegionOption[] _regions = FallbackRegions;
    private string _idKind = "Iqama";
    private string? _idDocumentName;
    private string? _avatarName;
    private string? _vipPhotoName;

    // The three file inputs use SimfFileUpload — the parent reads each control's
    // generated ElementId via JS interop and uploads the picked file after a
    // successful register-onsite (deferred pattern; needs the new user id).
    private SimfFileUpload _idDocUpload = default!;
    private SimfFileUpload _avatarUpload = default!;
    private SimfFileUpload _vipPhotoUpload = default!;

    // Option lists for the SimfSelect pickers. Enum-name / language-code option
    // values; the visible labels are localized at render (GenderLabel /
    // PreferredLanguageLabel).
    private static readonly string[] GenderCodes = { "Unspecified", "Male", "Female" };
    private static readonly string[] PreferredLanguages = { "ar", "en" };

    // Standard plate entry: three 17-letter picks (Latin codes) + a
    // digit field, assembled into _model.PlateNumber.
    private readonly string[] _plateLetters = { string.Empty, string.Empty, string.Empty };
    private string _plateDigits = string.Empty;

    // The required organisation (الجهة) typeahead state.
    private string _orgSearch = string.Empty;
    private string? _orgSelectedLabel;
    private string? _orgError;
    private bool _orgListOpen;
    private OrganisationPickerItem[] _orgResults = Array.Empty<OrganisationPickerItem>();
    private CancellationTokenSource? _orgSearchCts;

    /// <summary>"Visitor" or "Other" — drives the profile-type picker
    /// query and the interests section visibility.</summary>
    [Parameter] public string Kind { get; set; } = "Visitor";

    /// <summary>VIP mode: restrict the profile-type picker to the
    /// VVIP / VIP audience tiers and surface the موج (Mawj) welcome-message
    /// fields (Mawj ID, honorific, preferred language) + a separate VIP welcome
    /// photo. Off by default so the regular walk-in desk is unchanged.</summary>
    [Parameter] public bool VipMode { get; set; }

    /// <summary>When true (the delegates desk), the created visitor
    /// is flagged as a delegation member and the API requires an invited country.</summary>
    [Parameter] public bool IsDelegate { get; set; }

    /// <summary>The audience tier names the VIP page is restricted to.</summary>
    private static readonly string[] VipTierNames = { "VVIP", "VIP" };

    /// <summary>Fires after a successful register-onsite. The parent uses
    /// the response to render the post-submit success modal with the QR.</summary>
    [Parameter] public EventCallback<AdminWalkInRegistrationResponse> OnSuccess { get; set; }

    /// <summary>Optional cancel callback. When unset, no Cancel button.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _editContext = new EditContext(_model);
        // Surface per-field errors inline (the form owns the rules; the
        // ValidationMessageStore lets SimfTextField render them next to the field).
        _messages = new ValidationMessageStore(_editContext);

        // Load profile types for this kind + interests + countries +
        // organisations in parallel — the desk shouldn't wait for sequential
        // round-trips.
        // Every ProfileType is Visitor-scope; "Other" is no longer a valid
        // UserType, so fetching with userType=Other returned an EMPTY picker
        // (the Others desk showed "no profile types defined"). Fetch
        // under the valid Visitor scope and split audience vs partner client-side
        // by IsVisitor below; Kind still drives that split + the submit routing.
        // The desk mode decides which fields this form may demand. Fetched with
        // the rest rather than before them: a failure here leaves _quickRegister
        // false, so the form asks for everything and the desk is no worse off
        // than it was before quick mode existed.
        var deskModeTask = JS.InvokeAsync<ApiResult<WalkInDeskModeResponse>>(
            "simfAccount.getJson",
            "/account/api/admin/walk-in-mode/desk").AsTask();
        var profileTypesTask = JS.InvokeAsync<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
            "simfAccount.getJson",
            "/account/api/admin/profile-types?userType=Visitor").AsTask();
        var countriesTask = JS.InvokeAsync<ApiResult<CountryListResponse>>(
            "simfAccount.getJson",
            "/account/api/admin/walk-in/countries").AsTask();
        var organisationsTask = JS.InvokeAsync<ApiResult<IReadOnlyList<OrganisationPickerItem>>>(
            "simfAccount.getJson",
            "/account/api/admin/walk-in/organisations?top=20").AsTask();
        // Birth-region options from the DB-backed lookup. Active only
        // (isActive filter), a large page (Top=200, well above the 13 regions),
        // ordered by SortOrder server-side — the same source the app picker uses.
        var regionsTask = JS.InvokeAsync<ApiResult<GridPage<AdminRegionSummary>>>(
            "simfAccount.postJson",
            "/account/api/admin/regions/list",
            new GridQuery
            {
                Top = 200,
                Filters = new Dictionary<string, string> { ["isActive"] = "true" },
            }).AsTask();

        Task<ApiResult<InterestListResponse>>? interestsTask = null;
        if (string.Equals(Kind, "Visitor", StringComparison.OrdinalIgnoreCase))
        {
            interestsTask = JS.InvokeAsync<ApiResult<InterestListResponse>>(
                "simfAccount.getJson", "/account/api/interests").AsTask();
        }

        // A failed read leaves quick mode OFF, so the form demands the full set
        // and the desk behaves exactly as it did before the mode existed.
        var deskMode = await deskModeTask;
        if (deskMode is { Success: true, Data: not null })
        {
            _quickRegister = deskMode.Data.QuickRegister;
            _quickRequiresIdentityDocument = deskMode.Data.RequiresIdentityDocument;
        }

        ApplyProfileTypes(await profileTypesTask);

        var cEnvelope = await countriesTask;
        if (cEnvelope is { Success: true, Data: not null })
        {
            _countries = cEnvelope.Data.Countries.ToArray();
        }

        var oEnvelope = await organisationsTask;
        if (oEnvelope is { Success: true, Data: not null })
        {
            _orgResults = oEnvelope.Data.ToArray();
        }

        await ApplyRegionsAsync(regionsTask);

        if (interestsTask is not null)
        {
            var iEnvelope = await interestsTask;
            if (iEnvelope is { Success: true, Data: not null })
            {
                _interests = iEnvelope.Data.Interests.ToArray();
            }
        }
    }

    /// <summary>
    /// Shows only the profile types valid for this desk: audience types
    /// (IsVisitor=true) for the Visitor desk, partner types (IsVisitor=false) for
    /// the Other desk. Mirrors the server rule (expectedIsVisitor) + the approve
    /// modal's own filter, so the picker can no longer offer a type the server
    /// would reject.
    /// </summary>
    private void ApplyProfileTypes(ApiResult<IReadOnlyList<AdminProfileTypeSummary>>? envelope)
    {
        if (envelope is not { Success: true, Data: not null }) { return; }

        var visitorDesk = string.Equals(Kind, "Visitor", StringComparison.OrdinalIgnoreCase);
        _profileTypes = envelope.Data
            .Where(p => p.IsActive && p.IsVisitor == visitorDesk)
            // The VIP page only registers VVIP / VIP tiers.
            .Where(p => !VipMode || VipTierNames.Contains(p.Name))
            .OrderBy(p => p.Name).ToArray();

        // Preselect the tier when the picker is down to a single choice
        // (e.g. only VIP seeded), so the desk doesn't have to click it.
        if (VipMode && _profileTypes.Length == 1)
        {
            _model.ProfileTypeId = _profileTypes[0].Id;
        }
    }

    /// <summary>
    /// Swaps the birth-region options to the DB-backed lookup. Keeps the
    /// offline SaudiRegions fallback (already seeded into <c>_regions</c>) if the
    /// fetch fails or comes back empty, so the picker is never empty.
    /// </summary>
    private async Task ApplyRegionsAsync(Task<ApiResult<GridPage<AdminRegionSummary>>> regionsTask)
    {
        try
        {
            var envelope = await regionsTask;
            if (envelope is not { Success: true, Data: not null }
                || envelope.Data.Items.Count == 0)
            {
                return;
            }

            _regions = envelope.Data.Items
                .Where(r => r.IsActive)
                .Select(r => new RegionOption(r.Code, r.NameArabic, r.Name))
                .ToArray();
            if (_regions.Length == 0)
            {
                _regions = FallbackRegions;
            }
        }
        catch (Exception)
        {
            // Network / JS-interop failure — keep the offline fallback.
            _regions = FallbackRegions;
        }
    }

    private void SelectProfileType(Guid id) => _model.ProfileTypeId = id;

    private void SetIsSaudi(bool value)
    {
        _model.IsSaudi = value;
        // Clear the ID field that no longer applies so the validator
        // doesn't see stale data when staff toggles back and forth.
        if (value)
        {
            _model.IqamaNumber = null;
            _model.PassportNumber = null;
            // Saudi visitors: nationality is implicitly SA, no picker.
            _model.NationalityCode = "SA";
        }
        else
        {
            _model.NationalId = null;
            // Default to an empty pick so the placeholder shows.
            if (_model.NationalityCode == "SA")
            {
                _model.NationalityCode = string.Empty;
            }
            // Default the Iqama / Passport sub-picker to Iqama for fresh
            // non-Saudi state; staff can flip.
            _idKind = "Iqama";
        }
    }

    private void SetNonSaudiIdKind(string kind)
    {
        _idKind = kind;
        // Clear the other ID field so the wrong value never leaks to the
        // server payload if staff toggled after typing.
        if (kind == "Iqama") { _model.PassportNumber = null; }
        else { _model.IqamaNumber = null; }
    }

    private void OnNationalityPicked(CountryDto? country) =>
        _model.NationalityCode = country?.Code ?? string.Empty;

    private void OnDateOfBirthChanged(string raw) =>
        _model.DateOfBirth = string.IsNullOrEmpty(raw) || !DateOnly.TryParse(raw, out var parsed)
            ? null
            : parsed;

    // Gender picker (SimfSelect over enum-name option values); falls
    // back to Unspecified.
    private void OnGenderPicked(string? value) =>
        _model.Gender = Enum.TryParse<Gender>(value, out var g) ? g : Gender.Unspecified;

    private string GenderLabel(string code) => code switch
    {
        "Male" => L["Admin.WalkIn.Field.Gender.Male"],
        "Female" => L["Admin.WalkIn.Field.Gender.Female"],
        _ => L["Admin.WalkIn.Field.Gender.Unspecified"],
    };

    // Plate letter dropdowns + digit field assemble into the model;
    // the server validates SaudiPlate.IsValid and stores the canonical code.
    private void OnPlateLetterChanged(int index, ChangeEventArgs e)
    {
        _plateLetters[index] = e.Value?.ToString() ?? string.Empty;
        SyncPlate();
    }

    private void OnPlateDigitsChanged(ChangeEventArgs e)
    {
        _plateDigits = new string((e.Value?.ToString() ?? string.Empty)
            .Where(char.IsAsciiDigit).Take(4).ToArray());
        SyncPlate();
    }

    private void SyncPlate()
    {
        var letters = string.Concat(_plateLetters);
        _model.PlateNumber = letters.Length == 0 && _plateDigits.Length == 0
            ? null
            : $"{letters}{_plateDigits}";
    }

    // Birth-location region (Saudi only). The stored value is the
    // region's localized name (the existing free-text PlaceOfBirth column). The
    // option list is the DB-backed lookup (RegionOption) with the offline
    // SaudiRegions fallback; the English name can be blank for a DB row, so fall
    // back to the Arabic name when the UI is English but no English name exists.
    private static string RegionName(RegionOption region) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? region.NameArabic
            : (string.IsNullOrWhiteSpace(region.Name) ? region.NameArabic : region.Name!);

    // The <select> is keyed on the stable region code. Map a stored localized
    // place-of-birth name back to its option code so the dropdown survives a
    // UI-culture switch; "" (no match) leaves the placeholder selected.
    private string RegionCodeForName(string? placeOfBirth)
    {
        var n = placeOfBirth?.Trim() ?? string.Empty;
        if (n.Length == 0) { return string.Empty; }
        var match = _regions.FirstOrDefault(
            r => r.NameArabic == n
                 || (!string.IsNullOrWhiteSpace(r.Name) && r.Name == n));
        return match?.Code ?? string.Empty;
    }

    // The picker values are region codes; store the localized name back into the
    // free-text column so it round-trips identically to the app.
    private void OnBirthRegionPicked(RegionOption? region) =>
        _model.PlaceOfBirth = region is null ? string.Empty : RegionName(region);

    // The picked-file name (best-effort: browsers expose only the name for a
    // native file input). The actual upload happens after a successful
    // register-onsite — we need the new user id first. Each handler reads its
    // SimfFileUpload's generated element id.
    private async Task OnIdDocumentPicked() =>
        _idDocumentName = await PickedFileNameAsync(_idDocUpload);

    // Optional profile photo; uploaded after register-onsite
    // succeeds (same deferred pattern as the ID document — needs the new id).
    private async Task OnAvatarPicked() =>
        _avatarName = await PickedFileNameAsync(_avatarUpload);

    // VIP welcome photo; same deferred-upload pattern, posted to
    // the dedicated vip-photo endpoint after the account is created.
    private async Task OnVipPhotoPicked() =>
        _vipPhotoName = await PickedFileNameAsync(_vipPhotoUpload);

    private async Task<string?> PickedFileNameAsync(SimfFileUpload upload) =>
        await JS.InvokeAsync<string?>(
            "eval",
            $"document.getElementById('{upload.ElementId}')?.files?.[0]?.name ?? null");

    // Preferred-language picker (SimfSelect over language codes).
    private void OnPreferredLanguagePicked(string? value) =>
        _model.PreferredLanguage = string.IsNullOrWhiteSpace(value) ? null : value;

    private string PreferredLanguageLabel(string code) => code switch
    {
        "ar" => L["Admin.WalkIn.Field.PreferredLanguage.Ar"],
        "en" => L["Admin.WalkIn.Field.PreferredLanguage.En"],
        _ => code,
    };

    private void ToggleInterest(Guid id)
    {
        if (!_model.InterestIds.Remove(id))
        {
            _model.InterestIds.Add(id);
        }
    }

    private string LabelFor(AdminProfileTypeSummary pt) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? pt.NameArabic : pt.Name;

    // Saudi national ID is exactly 10 digits starting with 1; Iqama (residency
    // permit) starts with 2. Mirrors the server-side FluentValidation regex.
    private static readonly System.Text.RegularExpressions.Regex SaudiIdPattern =
        new("^1[0-9]{9}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex IqamaPattern =
        new("^2[0-9]{9}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string CountryLabel(CountryDto c) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? $"{c.NameArabic} ({c.Code})"
            : $"{c.Name} ({c.Code})";

    private static string InterestLabel(InterestDto i) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? i.NameArabic : i.Name;

    // Organisation (الجهة) typeahead. Prefer the Arabic name in
    // an Arabic UI; fall back to English when the Arabic name is blank.
    private static string OrgLabel(OrganisationPickerItem o) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? o.NameAr
            : (string.IsNullOrWhiteSpace(o.NameEn) ? o.NameAr : o.NameEn!);

    private void OpenOrgList() => _orgListOpen = true;

    private async Task OnOrgSearchInputAsync(ChangeEventArgs e)
    {
        _orgSearch = e.Value?.ToString() ?? string.Empty;
        _orgListOpen = true;
        // If the operator edits after picking, drop the selection until they
        // pick again — the field must always reflect a real chosen organisation.
        if (_orgSelectedLabel is not null
            && !string.Equals(_orgSearch, _orgSelectedLabel, StringComparison.Ordinal))
        {
            _model.OrganisationId = null;
            _orgSelectedLabel = null;
        }

        // Debounce — cancel the previous in-flight search before issuing a new
        // one so fast typing doesn't fan out a request per keystroke.
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

    private async Task SearchOrganisationsAsync(string? term)
    {
        var url = "/account/api/admin/walk-in/organisations?top=20";
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

    private void SelectOrganisation(OrganisationPickerItem org)
    {
        _model.OrganisationId = org.Id;
        _orgSelectedLabel = OrgLabel(org);
        _orgSearch = _orgSelectedLabel;
        _orgListOpen = false;
        _orgError = null;
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;

        if (!ValidateForm()) { return; }

        var basePath = string.Equals(Kind, "Other", StringComparison.OrdinalIgnoreCase)
            ? "/account/api/admin/others"
            : "/account/api/admin/visitors";

        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminWalkInRegistrationResponse>>(
                "simfAccount.postJson", $"{basePath}/register-onsite", BuildRegistrationRequest());

            if (envelope is { Success: true, Data: not null })
            {
                await UploadPickedFilesAsync(basePath, envelope.Data.UserId);
                await OnSuccess.InvokeAsync(envelope.Data);
                ResetForm();
            }
            else
            {
                _error = envelope?.Error?.DetailedMessageForCurrentCulture()
                    ?? L["Admin.WalkIn.Error.Fallback"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.WalkIn.Error.Fallback"];
        }
        finally { _busy = false; }
    }

    /// <summary>
    /// The friendly inline UX layer — server-side FluentValidation owns the
    /// canonical rule set. Field-level failures render next to the field
    /// (ValidationMessageStore); card / picker / phone-group errors surface in
    /// the top alert.
    /// </summary>
    /// <remarks>Every guard runs: the desk sees ALL of its mistakes at once
    /// rather than one per submit, so this must not early-return.</remarks>
    /// <summary>True when the desk is in quick mode, so this form may ask for
    /// the reduced field set. Defaults false: if the read fails the form demands
    /// everything, which is the behaviour that existed before quick mode.</summary>
    private bool _quickRegister;

    /// <summary>Whether the quick floor includes an identity document. Not
    /// admin-editable; it comes from deployment configuration.</summary>
    private bool _quickRequiresIdentityDocument = true;

    private bool ValidateForm()
    {
        _messages.Clear();
        _error = null;
        _orgError = null;

        // Quick mode: mirror the server's floor exactly (AdminAccountService
        // .EnsureQuickDeskFloor) - ANY one name, and ANY one identity document
        // when configuration still demands one. Asking for more here is what made
        // the mode unusable: the server would have accepted the short form, but
        // this form refused to submit it.
        if (_quickRegister)
        {
            return ValidateQuickFloor();
        }

        var ok = true;

        if (_model.ProfileTypeId == Guid.Empty)
        {
            _error = L["Admin.WalkIn.Error.ProfileTypeRequired"]; ok = false;
        }
        if (_model.EnglishName.Trim().Length is < 2 or > 128)
        {
            _messages.Add(_editContext.Field(nameof(_model.EnglishName)),
                L["Admin.WalkIn.Error.EnglishNameRequired"]); ok = false;
        }
        if (_model.ArabicName.Trim().Length is < 2 or > 128)
        {
            _messages.Add(_editContext.Field(nameof(_model.ArabicName)),
                L["Admin.WalkIn.Error.ArabicNameRequired"]); ok = false;
        }
        if (_model.DisplayName.Trim().Length is < 2 or > 128)
        {
            _messages.Add(_editContext.Field(nameof(_model.DisplayName)),
                L["Admin.WalkIn.Error.DisplayNameRequired"]); ok = false;
        }
        // The organisation (الجهة) is required; the error goes inline under the picker.
        if (_model.OrganisationId is null)
        {
            _orgError = L["Admin.WalkIn.Error.OrganisationRequired"]; ok = false;
        }

        var identityOk = ValidateIdentityDocument();
        ok = ok && identityOk;

        if (string.IsNullOrWhiteSpace(_model.SaudiMobile) && string.IsNullOrWhiteSpace(_model.InternationalMobile))
        {
            _error = L["Admin.WalkIn.Error.MobileRequired"]; ok = false;
        }

        _editContext.NotifyValidationStateChanged();
        return ok;
    }

    /// <summary>The quick-mode floor, mirroring the server's
    /// <c>EnsureQuickDeskFloor</c>. Deliberately NOT a relaxed copy of the full
    /// rules: the two must agree, and the cheapest way to keep them agreeing is
    /// for this to state the same two conditions and nothing else.</summary>
    private bool ValidateQuickFloor()
    {
        var hasName = !string.IsNullOrWhiteSpace(_model.ArabicName)
            || !string.IsNullOrWhiteSpace(_model.EnglishName)
            || !string.IsNullOrWhiteSpace(_model.DisplayName);
        if (!hasName)
        {
            _error = L["Admin.WalkIn.Error.QuickNameRequired"];
        }

        var hasDocument = !string.IsNullOrWhiteSpace(_model.NationalId)
            || !string.IsNullOrWhiteSpace(_model.IqamaNumber)
            || !string.IsNullOrWhiteSpace(_model.PassportNumber);
        if (hasName && _quickRequiresIdentityDocument && !hasDocument)
        {
            _error = L["Admin.WalkIn.Error.QuickDocumentRequired"];
        }

        _editContext.NotifyValidationStateChanged();
        return hasName && (!_quickRequiresIdentityDocument || hasDocument);
    }

    /// <summary>Validates the identity document for the Saudi / non-Saudi branch
    /// and clears whichever number the chosen branch does not use, so a value
    /// typed then switched away from is never submitted.</summary>
    private bool ValidateIdentityDocument()
    {
        var ok = true;

        if (_model.IsSaudi)
        {
            if (string.IsNullOrWhiteSpace(_model.NationalId)
                || !SaudiIdPattern.IsMatch(_model.NationalId))
            {
                _messages.Add(_editContext.Field(nameof(_model.NationalId)),
                    L["Admin.WalkIn.Error.NationalIdRequired"]); ok = false;
            }
            // Force nationality to SA — the picker is hidden in this branch.
            _model.NationalityCode = "SA";
            return ok;
        }

        if (string.IsNullOrWhiteSpace(_model.NationalityCode))
        {
            _error = L["Admin.WalkIn.Error.NationalityRequired"]; ok = false;
        }
        if (_idKind == "Iqama")
        {
            if (string.IsNullOrWhiteSpace(_model.IqamaNumber)
                || !IqamaPattern.IsMatch(_model.IqamaNumber))
            {
                _messages.Add(_editContext.Field(nameof(_model.IqamaNumber)),
                    L["Admin.WalkIn.Error.IqamaInvalid"]); ok = false;
            }
            _model.PassportNumber = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_model.PassportNumber)
                || _model.PassportNumber.Length > 20)
            {
                _messages.Add(_editContext.Field(nameof(_model.PassportNumber)),
                    L["Admin.WalkIn.Error.PassportRequired"]); ok = false;
            }
            _model.IqamaNumber = null;
        }
        return ok;
    }

    private AdminWalkInRegistrationRequest BuildRegistrationRequest() => new()
    {
        Email = NullIfBlank(_model.Email),
        DisplayName = _model.DisplayName.Trim(),
        ArabicName = _model.ArabicName.Trim(),
        EnglishName = _model.EnglishName.Trim(),
        JobTitle = NullIfBlank(_model.JobTitle),
        JobTitleArabic = NullIfBlank(_model.JobTitleArabic),
        // VIP موج extras (null unless the VIP page set them).
        MawjId = NullIfBlank(_model.MawjId),
        Honorific = NullIfBlank(_model.Honorific),
        HonorificArabic = NullIfBlank(_model.HonorificArabic),
        PreferredLanguage = NullIfBlank(_model.PreferredLanguage),
        ProfileTypeId = _model.ProfileTypeId,
        NationalityCode = _model.NationalityCode.Trim().ToUpperInvariant(),
        DateOfBirth = _model.DateOfBirth,
        PlaceOfBirth = NullIfBlank(_model.PlaceOfBirth),
        Gender = _model.Gender,
        PlateNumber = NullIfBlank(_model.PlateNumber)?.ToUpperInvariant(),
        IsSaudi = _model.IsSaudi,
        NationalId = _model.IsSaudi ? _model.NationalId : null,
        IqamaNumber = _model.IsSaudi ? null : _model.IqamaNumber,
        PassportNumber = _model.IsSaudi ? null : _model.PassportNumber,
        SaudiMobile = NullIfBlank(_model.SaudiMobile),
        InternationalMobile = NullIfBlank(_model.InternationalMobile),
        OrganisationId = _model.OrganisationId,
        InterestIds = _model.InterestIds.ToList(),
        // When hosted by the delegates page, mark the visitor as a
        // delegation member (the API then requires an invited nationality).
        IsDelegate = IsDelegate,
    };

    /// <summary>
    /// Uploads whichever optional files the desk picked, now that the new user
    /// id exists. Each upload is deliberately best-effort: the registration has
    /// already succeeded and a failed file must not undo it.
    /// </summary>
    private async Task UploadPickedFilesAsync(string basePath, Guid userId)
    {
        // A failure surfaces as HasIdImage=false in the View modal.
        await TryUploadAsync(_idDocumentName, $"{basePath}/{userId}/id-document", _idDocUpload.ElementId);
        // Optional profile photo.
        await TryUploadAsync(_avatarName, $"{basePath}/{userId}/avatar", _avatarUpload.ElementId);
        // Optional VIP welcome photo (VIP page only).
        if (VipMode)
        {
            await TryUploadAsync(_vipPhotoName, $"{basePath}/{userId}/vip-photo", _vipPhotoUpload.ElementId);
        }
    }

    private async Task TryUploadAsync(string? pickedName, string url, string elementId)
    {
        if (string.IsNullOrEmpty(pickedName)) { return; }
        try
        {
            await JS.InvokeAsync<object?>("simfAccount.uploadFile", url, elementId);
        }
        catch (Exception)
        {
            // Optional file; the registration stands either way.
        }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Clears the model to defaults so the desk can immediately
    /// register the next walk-in. Preserves the selected ProfileTypeId and the
    /// picked organisation because most walks-in to one desk share both.</summary>
    public void ResetForm()
    {
        var keepProfileTypeId = _model.ProfileTypeId;
        var keepIsSaudi = _model.IsSaudi;
        var keepNationality = _model.NationalityCode;
        var keepOrganisationId = _model.OrganisationId;
        var keepOrgLabel = _orgSelectedLabel;
        var keepOrgSearch = _orgSearch;
        _model.Email = string.Empty;
        _model.DisplayName = string.Empty;
        _model.ArabicName = string.Empty;
        _model.EnglishName = string.Empty;
        // Clear the VIP موج extras between desk registrations.
        _model.MawjId = null;
        _model.Honorific = null;
        _model.HonorificArabic = null;
        _model.PreferredLanguage = null;
        _model.DateOfBirth = null;
        _model.PlaceOfBirth = string.Empty;
        _model.NationalId = null;
        _model.IqamaNumber = null;
        _model.PassportNumber = null;
        _model.SaudiMobile = null;
        _model.InternationalMobile = null;
        _model.InterestIds.Clear();
        _model.ProfileTypeId = keepProfileTypeId;
        _model.IsSaudi = keepIsSaudi;
        _model.NationalityCode = keepNationality;
        _model.OrganisationId = keepOrganisationId;
        _orgSelectedLabel = keepOrgLabel;
        _orgSearch = keepOrgSearch;
        _orgError = null;
        _orgListOpen = false;
        _idDocumentName = null;
        _avatarName = null;
        _vipPhotoName = null;
        _plateLetters[0] = _plateLetters[1] = _plateLetters[2] = string.Empty;
        _plateDigits = string.Empty;
        _model.PlateNumber = null;
        _messages.Clear();
        _editContext.NotifyValidationStateChanged();
        StateHasChanged();
    }

    public void Dispose()
    {
        _orgSearchCts?.Cancel();
        _orgSearchCts?.Dispose();
    }

    // One birth-region option. Shared shape for both the DB-backed
    // lookup row (AdminRegionSummary) and the offline SaudiRegions fallback, so
    // the markup and the round-trip helpers don't branch on the source. Code is
    // the stable <option> value; Name (English) may be null for a DB row.
    private sealed record RegionOption(string Code, string NameArabic, string? Name);

    // The offline fallback — the same 13 official Saudi regions baked into
    // SIMF.Common.SaudiRegions. Used until the DB-backed list loads, and kept if
    // the fetch fails or returns nothing (owner decision: do not delete the
    // hardcoded list; it is the offline fallback).
    private static readonly RegionOption[] FallbackRegions =
        SaudiRegions.All
            .Select(r => new RegionOption(r.Code, r.Arabic, r.English))
            .ToArray();

    private sealed class Model
    {
        public string? Email { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string ArabicName { get; set; } = string.Empty;
        public string EnglishName { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? JobTitleArabic { get; set; }
        // VIP موج extras.
        public string? MawjId { get; set; }
        public string? Honorific { get; set; }
        public string? HonorificArabic { get; set; }
        public string? PreferredLanguage { get; set; }
        public Guid ProfileTypeId { get; set; }
        public string NationalityCode { get; set; } = "SA";
        public DateOnly? DateOfBirth { get; set; }
        public string PlaceOfBirth { get; set; } = string.Empty;
        public Gender Gender { get; set; } = Gender.Unspecified;
        public string? PlateNumber { get; set; }
        public bool IsSaudi { get; set; } = true;
        public string? NationalId { get; set; }
        public string? IqamaNumber { get; set; }
        public string? PassportNumber { get; set; }
        public string? SaudiMobile { get; set; }
        public string? InternationalMobile { get; set; }
        public Guid? OrganisationId { get; set; }
        public List<Guid> InterestIds { get; set; } = new();
    }
}
