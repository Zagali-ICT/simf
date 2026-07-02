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

public partial class CreateUser
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private Task OnCreatedAsync(AdminCreateUserResponse _)
    {
        // The child form shows its own success banner; the page leaves
        // navigation to the admin. (D-042 behaviour preserved.)
        return Task.CompletedTask;
    }

    private Task OnBackToListAsync()
    {
        Nav.NavigateTo("/admin/admins");
        return Task.CompletedTask;
    }
}
