using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class CreateVisitor
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private Task OnCreatedAsync(AdminCreateUserResponse _) => Task.CompletedTask;

    private Task OnBackToListAsync()
    {
        Nav.NavigateTo("/admin/visitors");
        return Task.CompletedTask;
    }
}
