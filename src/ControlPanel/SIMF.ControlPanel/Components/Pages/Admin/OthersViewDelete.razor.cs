using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class OthersViewDelete
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

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
