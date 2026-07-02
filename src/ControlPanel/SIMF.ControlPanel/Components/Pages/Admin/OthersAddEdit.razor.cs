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
using SIMF.Contracts.Faq;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class OthersAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    // Add path — bridge the wizard's AdminCreateUserResponse into the typed
    // OnSuccess the CrudShell host expects. The host only reads the saved row's
    // Email (for the create toast) and Id, so a minimal AdminUserProfileView
    // carrying those is sufficient; the grid reloads from the server next.
    private Task OnCreatedAsync(AdminCreateUserResponse created) =>
        OnSuccess.InvokeAsync(NewlyCreatedView(created));

    // Edit path — the EditAccountForm raises a parameterless OnSaved; replay the
    // (unchanged) Initial row so the host shows the edit-saved toast and reloads.
    private Task OnSavedInternalAsync() => OnSuccess.InvokeAsync(Initial);

    private static AdminUserProfileView NewlyCreatedView(AdminCreateUserResponse created) =>
        new(
            Id: created.UserId,
            Email: created.Email,
            DisplayName: string.Empty,
            UserType: string.Empty,
            AccountState: string.Empty,
            ProfileTypeId: null,
            ProfileTypeName: null,
            ProfileTypeNameArabic: null,
            ProfileTypeColor: null,
            QrId: null,
            ArabicName: null,
            EnglishName: null,
            JobTitle: null,
            NationalityCode: null,
            DateOfBirth: null,
            PlaceOfBirth: null,
            IsSaudi: false,
            NationalId: null,
            IqamaNumber: null,
            PassportNumber: null,
            SaudiMobile: null,
            InternationalMobile: null,
            HasIdImage: false,
            InterestIds: Array.Empty<Guid>(),
            RejectionReason: null,
            RejectionReasonArabic: null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: null);
}
