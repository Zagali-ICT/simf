// Tests: SIMF.ControlPanel.Tests/PermissionDeniedRoutingTests.cs

using Microsoft.AspNetCore.Components;

namespace SIMF.ControlPanel.Components;

/// <summary>
/// §6.16 (NAV-001) — a signed-in admin who reaches a page their role does not
/// permit is sent to the localized <c>/not-permitted</c> page.
///
/// <para>Previously <see cref="RedirectToLogin"/> handled every
/// <c>&lt;NotAuthorized&gt;</c> case, so a permission denial force-reloaded the
/// admin onto the sign-in form even though their cookie was perfectly valid —
/// which reads as "you were logged out", not "you may not open this page".
/// <c>NotPermitted.razor</c> was built for exactly this and was unreachable by
/// page routing: only <c>Program.cs</c>'s cookie <c>AccessDeniedPath</c> pointed
/// at it, which the Blazor interactive router never goes through.</para>
///
/// <para>Not a <c>forceLoad</c>: the circuit and the session are both healthy
/// here, so an ordinary client-side navigation is enough and keeps the shell.</para>
/// </summary>
public partial class RedirectToNotPermitted
{
    [Inject] private NavigationManager Nav { get; set; } = default!;

    protected override void OnInitialized() => Nav.NavigateTo("/not-permitted");
}
