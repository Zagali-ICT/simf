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

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class SponsorsAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // SponsorTier lives in SIMF.Domain (not referenced by the CP project), so
    // mirror its frozen value/name pairs here for the form dropdown. Keep
    // aligned with SIMF.Domain.Sponsors.SponsorTier.
    private sealed record TierOption(int Value, string Name);

    private static readonly IReadOnlyList<TierOption> _tiers =
    [
        new(10, "Platinum"),
        new(20, "Gold"),
        new(30, "Silver"),
        new(40, "Bronze"),
    ];

    private readonly Model _model = new();
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.NameEn = Initial.NameEn;
            _model.NameAr = Initial.NameAr;
            _model.Tier = Initial.Tier;
            _model.LogoRelativePath = Initial.LogoRelativePath ?? string.Empty;
            _model.Url = Initial.Url ?? string.Empty;
            _model.DisplayOrder = Initial.DisplayOrder;
            _model.Tagline = Initial.Tagline ?? string.Empty;
            _model.TaglineAr = Initial.TaglineArabic ?? string.Empty;
            _model.About = Initial.About ?? string.Empty;
            _model.AboutAr = Initial.AboutArabic ?? string.Empty;
            _model.IsActive = Initial.IsActive;
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.NameEn) || string.IsNullOrWhiteSpace(_model.NameAr))
        {
            _error = L["Admin.Sponsors.NameRequired"]; return;
        }

        _busy = true;
        try
        {
            ApiResult<AdminSponsorDetail>? envelope;
            if (!IsEdit)
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminSponsorDetail>>(
                    "simfAccount.postJson", "/account/api/admin/sponsors",
                    new AdminCreateSponsorRequest
                    {
                        NameEn = _model.NameEn.Trim(),
                        NameAr = _model.NameAr.Trim(),
                        Tier = _model.Tier,
                        LogoRelativePath = NullIfBlank(_model.LogoRelativePath),
                        Url = NullIfBlank(_model.Url),
                        DisplayOrder = _model.DisplayOrder,
                        Tagline = NullIfBlank(_model.Tagline),
                        TaglineArabic = NullIfBlank(_model.TaglineAr),
                        About = NullIfBlank(_model.About),
                        AboutArabic = NullIfBlank(_model.AboutAr),
                    });
            }
            else
            {
                envelope = await JS.InvokeAsync<ApiResult<AdminSponsorDetail>>(
                    "simfAccount.putJson", $"/account/api/admin/sponsors/{Initial!.Id}",
                    new AdminUpdateSponsorRequest
                    {
                        NameEn = _model.NameEn.Trim(),
                        NameAr = _model.NameAr.Trim(),
                        Tier = _model.Tier,
                        LogoRelativePath = NullIfBlank(_model.LogoRelativePath),
                        Url = NullIfBlank(_model.Url),
                        DisplayOrder = _model.DisplayOrder,
                        Tagline = NullIfBlank(_model.Tagline),
                        TaglineArabic = NullIfBlank(_model.TaglineAr),
                        About = NullIfBlank(_model.About),
                        AboutArabic = NullIfBlank(_model.AboutAr),
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
                    ?? L["Admin.Sponsors.Fallback"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Sponsors.Fallback"];
        }
        finally { _busy = false; }
    }

    private void OnTierChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n)) _model.Tier = n;
    }

    private void OnDisplayOrderChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var n)) _model.DisplayOrder = n;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class Model
    {
        public string NameEn { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public int Tier { get; set; } = 10;
        public string LogoRelativePath { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Tagline { get; set; } = string.Empty;
        public string TaglineAr { get; set; } = string.Empty;
        public string About { get; set; } = string.Empty;
        public string AboutAr { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
