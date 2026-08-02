using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class VisitorsViewDelete
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private AdminUserProfileView? _profile;
    private bool _loading = true;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        if (Initial is null)
        {
            _loading = false;
            return;
        }

        _loading = true;
        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AdminUserProfileView>>(
                "simfAccount.getJson",
                $"/account/api/admin/visitors/{Initial.Id}/profile");
            if (envelope is { Success: true, Data: not null })
            {
                _profile = envelope.Data;
            }
            else
            {
                _error = envelope?.Error?.MessageForCurrentCulture()
                    ?? L["Admin.Pending.View.Fallback"];
            }
        }
        catch (Exception)
        {
            _error = L["Admin.Pending.View.Fallback"];
        }
        finally { _loading = false; }
    }

    private string IdentityTypeLabel(AdminUserProfileView profile)
    {
        if (!string.IsNullOrEmpty(profile.NationalId))
            return L["Admin.Pending.View.IdentityType.National"];
        if (!string.IsNullOrEmpty(profile.IqamaNumber))
            return L["Admin.Pending.View.IdentityType.Iqama"];
        if (!string.IsNullOrEmpty(profile.PassportNumber))
            return L["Admin.Pending.View.IdentityType.Passport"];
        return L["Admin.Pending.View.MissingValue"];
    }

    private static string? IdentityNumber(AdminUserProfileView profile) =>
        profile.NationalId
            ?? profile.IqamaNumber
            ?? profile.PassportNumber;
}
