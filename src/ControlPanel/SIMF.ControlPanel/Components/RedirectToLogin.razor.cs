using Microsoft.AspNetCore.Components;

namespace SIMF.ControlPanel.Components;

public partial class RedirectToLogin
{
    [Inject] private NavigationManager Nav { get; set; } = default!;

    protected override void OnInitialized() =>
        Nav.NavigateTo("/login", forceLoad: true);
}
