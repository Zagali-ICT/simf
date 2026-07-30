using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using SIMF.Contracts.Authentication;

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
