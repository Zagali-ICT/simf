using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Organisations;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class OrganisationAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.NameAr = Initial.NameAr;
            _model.NameEn = Initial.NameEn ?? string.Empty;
            _model.CommercialRegistration = Initial.CommercialRegistration ?? string.Empty;
            _model.Sector = Initial.Sector ?? string.Empty;
            _model.City = Initial.City ?? string.Empty;
            _model.Phone = Initial.Phone ?? string.Empty;
            _model.Email = Initial.Email ?? string.Empty;
            _model.Website = Initial.Website ?? string.Empty;
            _model.IsActive = Initial.IsActive;
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        // Arabic name is the only required field (server-side FluentValidation
        // is the source of truth; this stops an obviously-empty submit).
        if (string.IsNullOrWhiteSpace(_model.NameAr))
        {
            _error = L["Admin.Organisations.Required"]; return;
        }

        _busy = true;
        try
        {
            ApiResult<AdminOrganisationDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminOrganisationDetail>>(
                    "simfAccount.postJson", "/account/api/admin/organisations",
                    new CreateOrganisationRequest
                    {
                        NameAr = _model.NameAr.Trim(),
                        NameEn = NullIfBlank(_model.NameEn),
                        CommercialRegistration = NullIfBlank(_model.CommercialRegistration),
                        Sector = NullIfBlank(_model.Sector),
                        City = NullIfBlank(_model.City),
                        Phone = NullIfBlank(_model.Phone),
                        Email = NullIfBlank(_model.Email),
                        Website = NullIfBlank(_model.Website),
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminOrganisationDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/organisations/{Initial!.Id}",
                    new UpdateOrganisationRequest
                    {
                        NameAr = _model.NameAr.Trim(),
                        NameEn = NullIfBlank(_model.NameEn),
                        CommercialRegistration = NullIfBlank(_model.CommercialRegistration),
                        Sector = NullIfBlank(_model.Sector),
                        City = NullIfBlank(_model.City),
                        Phone = NullIfBlank(_model.Phone),
                        Email = NullIfBlank(_model.Email),
                        Website = NullIfBlank(_model.Website),
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
                    ?? L["Admin.Organisations.LoadFailed"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Organisations.LoadFailed"];
        }
        finally { _busy = false; }
    }

    // Optional fields are nullable on the wire — send null rather than an empty
    // string when the admin leaves them blank.
    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class Model
    {
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string CommercialRegistration { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
