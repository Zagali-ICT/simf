using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Account;

public partial class Profile
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private SimfUserChrome UserChrome { get; set; } = default!;
    [Inject] private CpPreferences Prefs { get; set; } = default!;

    private ProfileResponse? _profile;
    private TotpSetupResponse? _setup;
    private bool _disablePrompt;
    private bool _busy;
    private string? _flashOk;
    private string? _flashError;
    private string? _passwordError;
    private string? _avatarError;
    private string? _avatarOk;
    private string? _displayFlash;
    // Cropper flow state.
    private bool _cropperOpen;
    private string _cropperImageUrl = string.Empty;
    private string _cropperFileName = "avatar.png";
    private IReadOnlyList<string>? _recoveryCodes;
    private readonly CodeModel _codeModel = new();
    private readonly CodeModel _disableModel = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        var envelope = await JS.InvokeAsync<ApiResult<ProfileResponse>>(
            "simfAccount.getJson", "/account/api/profile");
        if (envelope is { Success: true, Data: not null })
        {
            _profile = envelope.Data;
            // Push the avatar into the shared chrome so the top-bar avatar
            // re-renders immediately when the user uploads or removes one.
            UserChrome.SetAvatar(_profile.AvatarUrl);
        }
    }

    // -- 2FA enrolment --------------------------------------------------------

    private async Task StartSetupAsync()
    {
        ClearFlash();
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<TotpSetupResponse>>(
                "simfAccount.postJson", "/account/api/totp/setup", (object?)null);
            if (envelope is { Success: true, Data: not null })
            {
                _setup = envelope.Data;
                _codeModel.Code = string.Empty;
            }
            else
            {
                _flashError = envelope?.Error?.MessageForCurrentCulture();
            }
        }
        finally { _busy = false; }
    }

    private async Task ConfirmSetupAsync()
    {
        ClearFlash();
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<TotpConfirmResponse>>(
                "simfAccount.postJson", "/account/api/totp/confirm",
                new TotpConfirmRequest { Code = _codeModel.Code });
            if (envelope is { Success: true, Data: not null })
            {
                _setup = null;
                _flashOk = L["Profile.TwoFactor.ConfirmEnabled"];
                // Surface the fresh recovery-code batch returned by confirm
                // They're plaintext exactly once.
                _recoveryCodes = envelope.Data.RecoveryCodes;
                await LoadProfileAsync();
            }
            else
            {
                _flashError = envelope?.Error?.MessageForCurrentCulture();
            }
        }
        finally { _busy = false; }
    }

    private async Task RegenerateRecoveryCodesAsync()
    {
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<RecoveryCodesResponse>>(
                "simfAccount.postJson",
                "/account/api/recovery-codes/regenerate",
                (object?)null);
            if (envelope is { Success: true, Data: not null })
            {
                _recoveryCodes = envelope.Data.RecoveryCodes;
                await LoadProfileAsync();
            }
            else
            {
                _flashError = envelope?.Error?.MessageForCurrentCulture();
            }
        }
        finally { _busy = false; }
    }

    private void DismissRecoveryCodes() => _recoveryCodes = null;

    private void CancelSetup()
    {
        _setup = null;
        _codeModel.Code = string.Empty;
        ClearFlash();
    }

    private void StartDisableAsync()
    {
        _disablePrompt = true;
        _disableModel.Code = string.Empty;
        ClearFlash();
    }

    private void CancelDisable()
    {
        _disablePrompt = false;
        ClearFlash();
    }

    private async Task ConfirmDisableAsync()
    {
        ClearFlash();
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<TotpDisableResponse>>(
                "simfAccount.postJson", "/account/api/totp/disable",
                new TotpDisableRequest { Code = _disableModel.Code });
            if (envelope is { Success: true })
            {
                _disablePrompt = false;
                _flashOk = L["Profile.TwoFactor.ConfirmDisabled"];
                await LoadProfileAsync();
            }
            else
            {
                _flashError = envelope?.Error?.MessageForCurrentCulture();
            }
        }
        finally { _busy = false; }
    }

    // -- Change password ------------------------------------------------------

    private async Task ChangePasswordAsync(ChangePasswordCard.Model model)
    {
        _passwordError = null;
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<ChangePasswordResponse>>(
                "simfAccount.postJson", "/account/api/change-password",
                new ChangePasswordRequest
                {
                    CurrentPassword = model.CurrentPassword,
                    NewPassword = model.NewPassword,
                    ConfirmPassword = model.ConfirmPassword,
                });
            if (envelope is { Success: true })
            {
                // Changing the password rolls the security stamp and revokes
                // every session, so the next API call from this circuit would
                // fail. Submit the hidden sign-out form — /auth/sign-out is
                // POST-only, a GET via NavigateTo would 404 and leave the
                // cookie alive.
                await JS.InvokeVoidAsync("simfAccount.signOut");
            }
            else
            {
                _passwordError = envelope?.Error?.DetailedMessageForCurrentCulture();
            }
        }
        finally { _busy = false; }
    }

    // -- Avatar ---------------------------------------------------------------

    // File pick handler. Reads the file as a base64 data URL and
    // opens the cropper modal. No upload happens yet; the cropper's
    // OnCropped is what actually POSTs to the API.
    private async Task OnAvatarFilePickedAsync()
    {
        _avatarError = null;
        _avatarOk = null;
        try
        {
            var read = await JS.InvokeAsync<FileReadResult?>(
                "simfAccount.readFileAsDataUrl", "avatar-input");
            if (read is null || !read.Ok || string.IsNullOrEmpty(read.DataUrl))
            {
                _avatarError = L["Profile.Avatar.PickFailed"];
                return;
            }
            _cropperImageUrl = read.DataUrl;
            _cropperFileName = string.IsNullOrEmpty(read.FileName) ? "avatar.png" : read.FileName;
            _cropperOpen = true;
        }
        catch
        {
            _avatarError = L["Profile.Avatar.PickFailed"];
        }
    }

    // Cropper confirm handler. Receives the cropped 400×400 PNG as
    // a base64 data URL and uploads it via the existing avatar endpoint.
    private async Task OnAvatarCroppedAsync(string croppedDataUrl)
    {
        _cropperOpen = false;
        _avatarError = null;
        _avatarOk = null;
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AvatarResponse>>(
                "simfAccount.uploadDataUrl", "/account/api/avatar",
                croppedDataUrl, _cropperFileName);
            if (envelope is { Success: true, Data: not null })
            {
                await LoadProfileAsync();
                _avatarOk = L["Profile.Avatar.Updated"];
            }
            else
            {
                _avatarError = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Profile.Avatar.UploadFailed"];
            }
        }
        finally
        {
            _busy = false;
            _cropperImageUrl = string.Empty;
            // Clear the file input so picking the same file fires
            // the change event again. Uses clearFileInput (no click) — calling
            // triggerFileInput here would re-open the OS picker.
            await JS.InvokeVoidAsync("simfAccount.clearFileInput", "avatar-input");
        }
    }

    private Task OnAvatarCropCancelAsync()
    {
        _cropperOpen = false;
        _cropperImageUrl = string.Empty;
        return Task.CompletedTask;
    }

    // Shape of simfAccount.readFileAsDataUrl's JSON response.
    private sealed record FileReadResult(bool Ok, string? DataUrl, string? FileName, string? MimeType, string? Reason);

    // The avatar delete used to fire on the first click.
    private bool _confirmingAvatarRemove;

    private async Task ConfirmRemoveAvatarAsync()
    {
        _confirmingAvatarRemove = false;
        await RemoveAvatarAsync();
    }

    private async Task RemoveAvatarAsync()
    {
        if (_busy) return;
        _avatarError = null;
        _avatarOk = null;
        _busy = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AvatarResponse>>(
                "simfAccount.deleteJson", "/account/api/avatar");
            if (envelope is { Success: true })
            {
                await LoadProfileAsync();
                _avatarOk = L["Profile.Avatar.Removed"];
            }
            else
            {
                _avatarError = envelope?.Error?.MessageForCurrentCulture();
            }
        }
        finally { _busy = false; }
    }

    // -- Display preferences (D-353) ------------------------------------------

    // Clears every saved CP display/layout preference in this browser
    // (the CRUD dialog/full-page choices, and future grid layout). Pure
    // client-side localStorage wipe — no API, so no permission gate.
    private async Task ClearDisplayPrefsAsync()
    {
        _displayFlash = null;
        _busy = true;
        try
        {
            await Prefs.ClearAllAsync();
            _displayFlash = L["Profile.Display.Cleared"];
        }
        finally { _busy = false; }
    }

    // -- Misc -----------------------------------------------------------------

    private void ClearFlash()
    {
        _flashOk = null;
        _flashError = null;
    }

    private sealed class CodeModel
    {
        public string Code { get; set; } = string.Empty;
    }
}
