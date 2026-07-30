using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class CountryAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // D-499 (الوفود) — the "no head" sentinel prepended to the picker so an admin
    // can clear a previously-set head (the SimfSelect placeholder is disabled once
    // a value is chosen).
    private static readonly AdminCountryDelegateOption HeadNone =
        new(Guid.Empty, string.Empty, string.Empty, null);

    private readonly Model _model = new();
    private string _idInput = string.Empty;
    private string _displayOrderInput = "0";
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    // D-499 — the country's active delegates (head picker) + the current selection.
    private List<AdminCountryDelegateOption> _delegates = new();
    private AdminCountryDelegateOption? _selectedHead;
    private Guid? _initialHeadId;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Code = Initial.Code;
            _model.Name = Initial.Name;
            _model.NameArabic = Initial.NameArabic;
            _model.PhonePrefix = Initial.PhonePrefix ?? string.Empty;
            _model.IsActive = Initial.IsActive;
            _model.IsInvited = Initial.IsInvited;
            _model.ArrivalDate = Initial.DelegationArrivalDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            _model.DepartureDate = Initial.DelegationDepartureDate?.ToString("yyyy-MM-dd") ?? string.Empty;
            _initialHeadId = Initial.HeadOfDelegationUserProfileId;
            _displayOrderInput = Initial.DisplayOrder.ToString();
            _idInput = Initial.Id.ToString();
        }
        _editContext = new EditContext(_model);
    }

    protected override async Task OnInitializedAsync()
    {
        // D-499 — only the Edit form picks a head (a country has no delegates until
        // it has an id + registrations).
        if (!IsEdit || Initial is null) { return; }

        _delegates = new List<AdminCountryDelegateOption> { HeadNone };
        var envelope = await JS.InvokeAsync<ApiResult<IReadOnlyList<AdminCountryDelegateOption>>>(
            "simfAccount.getJson", $"/account/api/admin/countries/{Initial.Id}/delegates");
        if (envelope is { Success: true, Data: not null })
        {
            _delegates.AddRange(envelope.Data);
        }
        _selectedHead = _initialHeadId is { } id
            ? _delegates.FirstOrDefault(d => d.UserProfileId == id) ?? HeadNone
            : HeadNone;
    }

    private string HeadLabel(AdminCountryDelegateOption option)
    {
        if (option.UserProfileId == Guid.Empty) { return L["Admin.Countries.Field.HeadNone"]; }
        var name = string.IsNullOrWhiteSpace(option.NameArabic) ? option.Name : option.NameArabic;
        return string.IsNullOrWhiteSpace(option.JobTitle) ? name : $"{name} — {option.JobTitle}";
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        int countryId = Initial?.Id ?? 0;
        if (!IsEdit)
        {
            // Create mode — admin types the ISO 3166-1 numeric.
            if (!int.TryParse(_idInput, out countryId) || countryId is <= 0 or > 999)
            {
                _error = L["Admin.Countries.Field.IdInvalid"]; return;
            }
        }
        if (string.IsNullOrWhiteSpace(_model.Code) || _model.Code.Trim().Length != 2)
        {
            _error = L["Admin.Countries.Field.CodeInvalid"]; return;
        }
        if (string.IsNullOrWhiteSpace(_model.Name) || _model.Name.Length > 128)
        {
            _error = L["Admin.Countries.Field.NameEnInvalid"]; return;
        }
        if (string.IsNullOrWhiteSpace(_model.NameArabic) || _model.NameArabic.Length > 128)
        {
            _error = L["Admin.Countries.Field.NameArInvalid"]; return;
        }
        if (!int.TryParse(_displayOrderInput, out var order) || order < 0)
        {
            _error = L["Admin.Countries.Field.DisplayOrderInvalid"]; return;
        }

        // D-499 — parse the optional delegation date range (HTML date inputs post
        // an invariant yyyy-MM-dd string) and resolve the head pointer.
        var arrival = ParseDate(_model.ArrivalDate);
        var departure = ParseDate(_model.DepartureDate);
        if (arrival is not null && departure is not null && departure < arrival)
        {
            _error = L["Admin.Countries.Field.DelegationDatesInvalid"]; return;
        }
        var headId = _selectedHead is { } head && head.UserProfileId != Guid.Empty
            ? head.UserProfileId : (Guid?)null;

        _busy = true;
        try
        {
            ApiResult<AdminCountryDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminCountryDetail>>(
                    "simfAccount.postJson", "/account/api/admin/countries",
                    new AdminCreateCountryRequest
                    {
                        Id = countryId,
                        Code = _model.Code.Trim().ToUpperInvariant(),
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        PhonePrefix = NullIfBlank(_model.PhonePrefix),
                        DisplayOrder = order,
                        IsInvited = _model.IsInvited,
                        DelegationArrivalDate = arrival,
                        DelegationDepartureDate = departure,
                        HeadOfDelegationUserProfileId = headId,
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminCountryDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/countries/{Initial!.Id}",
                    new AdminUpdateCountryRequest
                    {
                        Code = _model.Code.Trim().ToUpperInvariant(),
                        Name = _model.Name.Trim(),
                        NameArabic = _model.NameArabic.Trim(),
                        PhonePrefix = NullIfBlank(_model.PhonePrefix),
                        DisplayOrder = order,
                        IsActive = _model.IsActive,
                        IsInvited = _model.IsInvited,
                        DelegationArrivalDate = arrival,
                        DelegationDepartureDate = departure,
                        HeadOfDelegationUserProfileId = headId,
                    });
            }

            if (envelope is { Success: true, Data: not null })
            {
                await OnSuccess.InvokeAsync(envelope.Data);
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Countries.Fallback"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Countries.Fallback"];
        }
        finally { _busy = false; }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact((value ?? string.Empty).Trim(), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date : null;

    private sealed class Model
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public string PhonePrefix { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsInvited { get; set; }
        public string ArrivalDate { get; set; } = string.Empty;
        public string DepartureDate { get; set; } = string.Empty;
    }
}
