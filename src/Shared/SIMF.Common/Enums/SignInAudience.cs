using System.ComponentModel.DataAnnotations;
using SIMF.Common.Resources.Enums;

namespace SIMF.Common.Enums;

/// <summary>
/// The surface a sign-in attempt came from (P2). The API enforces that only
/// CP-roled users sign in from <see cref="Cp"/> and only visitors sign in
/// from <see cref="Web"/> / <see cref="App"/>; a mismatch returns 403
/// <c>AUTH_WRONG_SURFACE_*</c>.
/// </summary>
public enum SignInAudience
{
    /// <summary>The visitor Website (Blazor SSR). Default — least privileged.</summary>
    [Display(Description = nameof(ResSignInAudience.Web), ResourceType = typeof(ResSignInAudience))]
    Web = 0,

    /// <summary>The Control Panel (Blazor Server) — staff / admin only.</summary>
    [Display(Description = nameof(ResSignInAudience.Cp), ResourceType = typeof(ResSignInAudience))]
    Cp = 1,

    /// <summary>The Flutter mobile app — visitors only, same rule as Web.</summary>
    [Display(Description = nameof(ResSignInAudience.App), ResourceType = typeof(ResSignInAudience))]
    App = 2,
}
