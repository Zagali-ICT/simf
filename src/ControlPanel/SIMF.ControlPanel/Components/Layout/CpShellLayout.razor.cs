using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Components.Forms;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.Contracts.Notifications;

namespace SIMF.ControlPanel.Components.Layout;

public partial class CpShellLayout
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private SimfUserChrome UserChrome { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;

    // Issue-1 — the signed-in user's permission codes (copied from the JWT into
    // the cookie at sign-in). The side menu hides items the user lacks; an
    // Administrator carries the wildcard "*" and sees everything. Defaults to
    // an empty set so the brief pre-auth render is fail-closed.
    private HashSet<string> _permissions = new(StringComparer.Ordinal);
    private bool _hasAllPermissions;

    // True when the user may see a nav item: ungated items (RequiredPermission
    // null — the dashboard + not-yet-built stubs) are always shown; otherwise
    // the user must hold the code (or the Administrator wildcard).
    private bool CanSee(CpNavigation.NavItem item) =>
        item.RequiredPermission is null
        || _hasAllPermissions
        || _permissions.Contains(item.RequiredPermission);

    // The live side-menu filter (the search box at the top of the nav). Empty =
    // show everything; otherwise an item is kept when its resolved label
    // contains the query (culture-aware, case-insensitive), so it works in both
    // English and Arabic.
    private string _navFilter = string.Empty;

    private bool MatchesFilter(CpNavigation.NavItem item) =>
        string.IsNullOrWhiteSpace(_navFilter)
        || L[item.LabelKey].Value.Contains(
               _navFilter.Trim(), StringComparison.CurrentCultureIgnoreCase);

    private bool GroupLabelMatches(CpNavigation.NavGroup group) =>
        !string.IsNullOrWhiteSpace(_navFilter)
        && L[group.LabelKey].Value.Contains(
               _navFilter.Trim(), StringComparison.CurrentCultureIgnoreCase);

    protected override async Task OnInitializedAsync()
    {
        // P11 — D-052: guard every CP shell page. A non-Approved user is
        // routed to the matching state-banner page before any module
        // content renders. The pending / rejected pages use MainLayout
        // (not this layout), so they don't trigger this guard themselves.
        var auth = await AuthState.GetAuthenticationStateAsync();

        // Issue-1 — snapshot the permission claims for the side-menu filter.
        _permissions = auth.User.FindAll(PermissionCatalog.ClaimType)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);
        _hasAllPermissions = _permissions.Contains(PermissionCatalog.Wildcard);

        var state = auth.User.FindFirst("account_state")?.Value;
        if (string.Equals(state, "PendingApproval", StringComparison.Ordinal))
        {
            Nav.NavigateTo("/auth/pending", forceLoad: false);
            return;
        }
        if (string.Equals(state, "Rejected", StringComparison.Ordinal))
        {
            Nav.NavigateTo("/auth/rejected", forceLoad: false);
            return;
        }

        // Re-render whenever the profile page (or anyone else) updates the
        // user chrome so the top-bar avatar tracks the profile in real time.
        UserChrome.Changed += OnChromeChanged;

        // Lazy-load the avatar on the first time a signed-in circuit boots —
        // the profile page would do it on /account/profile, but a user who
        // never opens that page should still see their avatar in the top bar.
        if (UserChrome.AvatarUrl is null)
        {
            try
            {
                var envelope = await JS.InvokeAsync<ApiResult<ProfileResponse>>(
                    "simfAccount.getJson", "/account/api/profile");
                if (envelope is { Success: true, Data: not null })
                {
                    UserChrome.SetAvatar(envelope.Data.AvatarUrl);
                }
            }
            catch (JSException)
            {
                // Best-effort — a failed prefetch leaves the placeholder icon.
            }
        }
    }

    private void OnChromeChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => UserChrome.Changed -= OnChromeChanged;
}
