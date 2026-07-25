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
    private bool _busy;
    private string? _error;

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
            _form.IsActive = Initial.IsActive;
        }
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
        public bool IsActive { get; set; } = true;
    }
}
