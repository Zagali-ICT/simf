using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class EditAccountForm
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The account being edited.</summary>
    [Parameter, EditorRequired] public Guid AccountId { get; set; }

    /// <summary>"visitors" or "others" — drives the route + scope.</summary>
    [Parameter, EditorRequired] public string Scope { get; set; } = "visitors";

    /// <summary>True for the audience (Visitor) desk; false for the partner (Other) desk.</summary>
    [Parameter] public bool IsVisitorScope { get; set; } = true;

    /// <summary>When true, the form also offers the VVIP/VIP
    /// welcome photo (موج) field. Set by the VIP list; the visitors/others desks
    /// leave it false. Only meaningful for the visitors scope (the vip-photo
    /// endpoint lives under /admin/visitors only).</summary>
    [Parameter] public bool ShowVipPhoto { get; set; }

    /// <summary>Raised after a successful save.</summary>
    [Parameter] public EventCallback OnSaved { get; set; }

    /// <summary>Raised when the admin cancels.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    // DOM ids for the three optional image inputs. Only one edit form is open at
    // a time (hosted in a CrudShell popup / full page or the VIP modal), so fixed
    // ids are safe and let simfAccount.uploadFile read the picked file.
    private const string AvatarInputId = "edit-account-avatar";
    private const string IdDocumentInputId = "edit-account-id-document";
    private const string VipPhotoInputId = "edit-account-vip-photo";

    private string _email = string.Empty;
    private string _displayName = string.Empty;
    private Guid? _profileTypeId;
    private IReadOnlyList<AdminProfileTypeSummary> _profileTypes = new List<AdminProfileTypeSummary>();

    // Bi-Meeting rework — the two admin-assigned per-user meeting-eligibility flags.
    private bool _allowsSpeakerMeeting;
    private bool _allowsDelegationMeeting;

    // The nationality picker. `_nationalityCode` is the ISO alpha-2 code sent on
    // save; empty means "leave the stored nationality alone" (the server treats an
    // empty/absent code as no change). Prefilled from the loaded profile.
    private IReadOnlyList<CountryDto> _countries = new List<CountryDto>();
    private string _nationalityCode = string.Empty;

    // The mobile correction. Prefilled from the loaded profile;
    // an empty field sends nothing and leaves the stored number untouched.
    private string _saudiMobile = string.Empty;
    private string _internationalMobile = string.Empty;

    private bool _loading = true;
    private bool _busy;
    private string? _loadError;
    private string? _saveError;

    // Current-photo indicators from the loaded profile, so we only render a
    // preview when an image is actually on file (never a broken <img>/404).
    private bool _hasAvatar;
    private bool _hasIdImage;
    // Cache-buster fixed at load time so the previews of the existing images are
    // stable while the form is open.
    private long _cacheBust;

    // Whether the admin picked a new file for each optional image this session.
    private bool _avatarPicked;
    private bool _idDocumentPicked;
    private bool _vipPhotoPicked;

    private bool VipPhotoEnabled => ShowVipPhoto && IsVisitorScope;

    private string AvatarPreviewUrl =>
        $"/account/api/admin/{Scope}/{AccountId}/avatar?v={_cacheBust}";

    private string IdImagePreviewUrl =>
        $"/account/api/admin/{Scope}/{AccountId}/id-document?v={_cacheBust}";

    private AdminProfileTypeSummary? _selectedProfileType =>
        _profileTypes.FirstOrDefault(p => p.Id == _profileTypeId);

    private bool CanSave =>
        !_busy
        && !string.IsNullOrWhiteSpace(_email)
        && _displayName.Trim().Length >= 2
        && (IsVisitorScope || _profileTypeId is not null);

    private void OnProfileTypeChanged(AdminProfileTypeSummary? selected) =>
        _profileTypeId = selected?.Id;

    // Nationality picker plumbing. Mirrors the walk-in desk's field so the two
    // admin surfaces label and pick a country identically.
    private CountryDto? _selectedCountry =>
        _countries.FirstOrDefault(c => c.Code == _nationalityCode);

    private void OnNationalityPicked(CountryDto? country) =>
        _nationalityCode = country?.Code ?? string.Empty;

    // Null when nothing is picked, so the server leaves the stored nationality alone.
    private string? NationalityCodeForSave =>
        string.IsNullOrWhiteSpace(_nationalityCode) ? null : _nationalityCode;

    // FR-PHN-002 — an empty field means "no change" (the server's contract), so a
    // desk correcting only the email never wipes the number.
    private string? SaudiMobileForSave =>
        string.IsNullOrWhiteSpace(_saudiMobile) ? null : _saudiMobile.Trim();

    private string? InternationalMobileForSave =>
        string.IsNullOrWhiteSpace(_internationalMobile) ? null : _internationalMobile.Trim();

    private static string CountryLabel(CountryDto c) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"
            ? $"{c.NameArabic} ({c.Code})"
            : $"{c.Name} ({c.Code})";

    protected override async Task OnInitializedAsync()
    {
        _loading = true;
        _cacheBust = SimfClock.Now.Ticks;
        try
        {
            await LoadProfileTypesAsync();
            await LoadCountriesAsync();
            await LoadAccountAsync();
        }
        catch (Exception)
        {
            _loadError = L["Admin.Edit.LoadFailed"];
        }
        finally { _loading = false; }
    }

    private async Task LoadProfileTypesAsync()
    {
        // Both Visitor and Other profile types are UserType=Visitor;
        // the picker route filters by UserType, then we narrow to the scope's
        // IsVisitor side so the dropdown only offers valid tiers.
        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
            "simfAccount.getJson", "/account/api/admin/profile-types?userType=Visitor");
        if (envelope is { Success: true, Data: not null })
        {
            _profileTypes = envelope.Data
                .Where(p => p.IsActive && p.IsVisitor == IsVisitorScope)
                .ToList();
        }
    }

    // The same active-country list the walk-in desk's nationality dropdown uses,
    // through the existing BFF passthrough (no new proxy route). Nationality is an
    // OPTIONAL field on this form, so a failed country read must not block the edit:
    // swallow it, leave the picker empty (which sends no nationality and keeps the
    // stored value) and let the admin still fix the email / name / tier.
    private async Task LoadCountriesAsync()
    {
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<CountryListResponse>>(
                "simfAccount.getJson", "/account/api/admin/walk-in/countries");
            if (envelope is { Success: true, Data: not null })
            {
                _countries = envelope.Data.Countries;
            }
        }
        catch (Exception)
        {
            _countries = new List<CountryDto>();
        }
    }

    private async Task LoadAccountAsync()
    {
        var envelope = await JS.InvokeAsync<ApiResult<AdminUserProfileView>>(
            "simfAccount.getJson", $"/account/api/admin/{Scope}/{AccountId}/profile");
        if (envelope is { Success: true, Data: not null })
        {
            _email = envelope.Data.Email;
            _displayName = envelope.Data.DisplayName;
            _profileTypeId = envelope.Data.ProfileTypeId;
            _allowsSpeakerMeeting = envelope.Data.AllowsSpeakerMeeting;
            _allowsDelegationMeeting = envelope.Data.AllowsDelegationMeeting;
            _hasAvatar = envelope.Data.HasAvatar;
            _hasIdImage = envelope.Data.HasIdImage;
            _nationalityCode = envelope.Data.NationalityCode ?? string.Empty;
            _saudiMobile = envelope.Data.SaudiMobile ?? string.Empty;
            _internationalMobile = envelope.Data.InternationalMobile ?? string.Empty;
        }
        else
        {
            _loadError = envelope?.Error?.MessageForCurrentCulture()
                ?? L["Admin.Edit.LoadFailed"];
        }
    }

    // Record that the admin picked a new file for one of the optional images, so
    // SaveAsync knows to upload it. Reads only the file name (presence check) —
    // the bytes stay in the DOM input and are streamed by simfAccount.uploadFile.
    private async Task OnAvatarPickedAsync() =>
        _avatarPicked = await HasPickedFileAsync(AvatarInputId);

    private async Task OnIdDocumentPickedAsync() =>
        _idDocumentPicked = await HasPickedFileAsync(IdDocumentInputId);

    private async Task OnVipPhotoPickedAsync() =>
        _vipPhotoPicked = await HasPickedFileAsync(VipPhotoInputId);

    private async Task<bool> HasPickedFileAsync(string inputId)
    {
        var name = await JS.InvokeAsync<string?>(
            "eval", $"document.getElementById('{inputId}')?.files?.[0]?.name ?? null");
        return !string.IsNullOrEmpty(name);
    }

    private async Task SaveAsync()
    {
        if (!CanSave) return;
        _busy = true;
        _saveError = null;
        try
        {
            object body = IsVisitorScope
                ? new AdminUpdateVisitorRequest
                {
                    Email = _email.Trim(),
                    DisplayName = _displayName.Trim(),
                    ProfileTypeId = _profileTypeId,
                    AllowsSpeakerMeeting = _allowsSpeakerMeeting,
                    AllowsDelegationMeeting = _allowsDelegationMeeting,
                    NationalityCode = NationalityCodeForSave,
                    SaudiMobile = SaudiMobileForSave,
                    InternationalMobile = InternationalMobileForSave,
                }
                : new AdminUpdateOtherRequest
                {
                    Email = _email.Trim(),
                    DisplayName = _displayName.Trim(),
                    ProfileTypeId = _profileTypeId ?? Guid.Empty,
                    AllowsSpeakerMeeting = _allowsSpeakerMeeting,
                    AllowsDelegationMeeting = _allowsDelegationMeeting,
                    NationalityCode = NationalityCodeForSave,
                    SaudiMobile = SaudiMobileForSave,
                    InternationalMobile = InternationalMobileForSave,
                };

            var envelope = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.putJson", $"/account/api/admin/{Scope}/{AccountId}", body);
            if (envelope is not { Success: true })
            {
                _saveError = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Edit.Fallback"];
                return;
            }

            // Core fields (email + name + tier) saved. Now upload any newly-picked
            // images to the existing account-id-keyed admin endpoints. A failed
            // image upload keeps the form open with the reason (e.g. the ID
            // face-gate) so the admin can re-pick — the saved core fields stand.
            var imageError = await UploadPickedImagesAsync();
            if (imageError is not null)
            {
                _saveError = imageError;
                return;
            }

            await OnSaved.InvokeAsync();
        }
        finally { _busy = false; }
    }

    /// <summary>Uploads each picked image via the same multipart proxy the
    /// walk-in form uses. Returns the first failure message (bilingual,
    /// current-culture) or <c>null</c> when every upload succeeded / none picked.</summary>
    private async Task<string?> UploadPickedImagesAsync()
    {
        if (_avatarPicked)
        {
            var result = await JS.InvokeAsync<ApiResult<AvatarResponse>>(
                "simfAccount.uploadFile",
                $"/account/api/admin/{Scope}/{AccountId}/avatar", AvatarInputId);
            if (result is not { Success: true })
            {
                return $"{L["Admin.Edit.Photo.Avatar"]}: "
                    + (result?.Error?.MessageForCurrentCulture() ?? L["Admin.Edit.Fallback"]);
            }
        }

        if (_idDocumentPicked)
        {
            var result = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.uploadFile",
                $"/account/api/admin/{Scope}/{AccountId}/id-document", IdDocumentInputId);
            if (result is not { Success: true })
            {
                return $"{L["Admin.Edit.Photo.IdDocument"]}: "
                    + (result?.Error?.MessageForCurrentCulture() ?? L["Admin.Edit.Fallback"]);
            }
        }

        if (VipPhotoEnabled && _vipPhotoPicked)
        {
            var result = await JS.InvokeAsync<ApiResult<bool>>(
                "simfAccount.uploadFile",
                $"/account/api/admin/visitors/{AccountId}/vip-photo", VipPhotoInputId);
            if (result is not { Success: true })
            {
                return $"{L["Admin.Edit.Photo.VipPhoto"]}: "
                    + (result?.Error?.MessageForCurrentCulture() ?? L["Admin.Edit.Fallback"]);
            }
        }

        return null;
    }
}
