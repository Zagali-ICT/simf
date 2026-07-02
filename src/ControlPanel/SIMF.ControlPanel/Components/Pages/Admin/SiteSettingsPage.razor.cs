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
using SIMF.Contracts.Sessions;
using SIMF.Contracts.Logs;
using SIMF.Contracts.UserProfile;
using SIMF.Contracts.Gates;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Notifications;
using SIMF.Contracts.Configuration;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SiteSettingsPage
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private bool _loading = true;
    private bool _busy;
    private string? _toastMessage;
    private string _toastVariant = "success";

    protected override async Task OnInitializedAsync()
    {
        _editContext = new EditContext(_model);
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<SiteSettingsResponse>>(
                "simfAccount.getJson", "/account/api/admin/site-settings");
            if (envelope is { Success: true, Data: not null })
            {
                var d = envelope.Data;
                _model.RegistrationMessageAr = d.RegistrationSuccessMessageAr;
                _model.RegistrationMessageEn = d.RegistrationSuccessMessageEn;
                _model.Facebook = d.Social.Facebook ?? string.Empty;
                _model.X = d.Social.X ?? string.Empty;
                _model.Instagram = d.Social.Instagram ?? string.Empty;
                _model.LinkedIn = d.Social.LinkedIn ?? string.Empty;
                _model.YouTube = d.Social.YouTube ?? string.Empty;
                _model.TikTok = d.Social.TikTok ?? string.Empty;
                _model.Snapchat = d.Social.Snapchat ?? string.Empty;
            }
            else
            {
                _toastVariant = "error";
                _toastMessage = L["Admin.SiteSettings.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _toastVariant = "error";
            _toastMessage = L["Admin.SiteSettings.LoadFailed"];
        }
        finally { _loading = false; }
    }

    private async Task SaveAsync()
    {
        if (_busy) { return; }
        _busy = true;
        _toastMessage = null;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<SiteSettingsResponse>>(
                "simfAccount.putJson", "/account/api/admin/site-settings",
                new AdminUpdateSiteSettingsRequest
                {
                    RegistrationMessageAr = _model.RegistrationMessageAr,
                    RegistrationMessageEn = _model.RegistrationMessageEn,
                    Facebook = _model.Facebook,
                    X = _model.X,
                    Instagram = _model.Instagram,
                    LinkedIn = _model.LinkedIn,
                    YouTube = _model.YouTube,
                    TikTok = _model.TikTok,
                    Snapchat = _model.Snapchat,
                });
            if (envelope is { Success: true })
            {
                _toastVariant = "success";
                _toastMessage = L["Admin.SiteSettings.Saved"];
            }
            else
            {
                _toastVariant = "error";
                _toastMessage = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.SiteSettings.SaveFailed"];
            }
        }
        catch (Exception)
        {
            _toastVariant = "error";
            _toastMessage = L["Admin.SiteSettings.SaveFailed"];
        }
        finally { _busy = false; }
    }

    private sealed class Model
    {
        public string RegistrationMessageAr { get; set; } = string.Empty;
        public string RegistrationMessageEn { get; set; } = string.Empty;
        public string Facebook { get; set; } = string.Empty;
        public string X { get; set; } = string.Empty;
        public string Instagram { get; set; } = string.Empty;
        public string LinkedIn { get; set; } = string.Empty;
        public string YouTube { get; set; } = string.Empty;
        public string TikTok { get; set; } = string.Empty;
        public string Snapchat { get; set; } = string.Empty;
    }
}
