using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Contracts.Admin;
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
                _model.PartnerDirectoryEnabled = d.PartnerDirectoryEnabled;
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
                    PartnerDirectoryEnabled = _model.PartnerDirectoryEnabled,
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

        // Build #13 — the "Meet People Like You" partner-directory switch.
        public bool PartnerDirectoryEnabled { get; set; } = true;
    }
}
