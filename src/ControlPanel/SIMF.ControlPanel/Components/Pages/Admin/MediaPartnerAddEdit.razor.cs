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
using SIMF.Contracts.PublicRelations;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class MediaPartnerAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private string _displayOrderInput = "0";
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Name = Initial.Name;
            _model.NameArabic = Initial.NameArabic;
            _model.LogoRelativePath = Initial.LogoRelativePath ?? string.Empty;
            _model.Url = Initial.Url ?? string.Empty;
            _model.ContactId = Initial.ContactId;
            _model.IsActive = Initial.IsActive;
            _displayOrderInput = Initial.DisplayOrder.ToString();
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Name) || string.IsNullOrWhiteSpace(_model.NameArabic))
        {
            _error = L["Admin.MediaPartners.NameRequired"]; return;
        }
        if (!int.TryParse(_displayOrderInput, out var order) || order < 0)
        {
            order = 0;
        }

        _busy = true;
        try
        {
            ApiResult<AdminMediaPartnerDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminMediaPartnerDetail>>(
                    "simfAccount.postJson", "/account/api/admin/media-partners",
                    new AdminCreateMediaPartnerRequest(
                        _model.Name.Trim(),
                        _model.NameArabic.Trim(),
                        NullIfBlank(_model.LogoRelativePath),
                        NullIfBlank(_model.Url),
                        order,
                        _model.ContactId));
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminMediaPartnerDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/media-partners/{Initial!.Id}",
                    new AdminUpdateMediaPartnerRequest
                    {
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        LogoRelativePath = NullIfBlank(_model.LogoRelativePath),
                        Url = NullIfBlank(_model.Url),
                        DisplayOrder = order,
                        ContactId = _model.ContactId,
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
                    ?? L["Admin.MediaPartners.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.MediaPartners.LoadFailed"];
        }
        finally { _busy = false; }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class Model
    {
        public string Name { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public string LogoRelativePath { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public Guid? ContactId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
