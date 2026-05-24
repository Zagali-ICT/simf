namespace SIMF.Contracts.Authentication;

/// <summary>
/// The surface a sign-in attempt came from (P2). The API enforces that only
/// CP-roled users sign in from <see cref="Cp"/> and only visitors sign in
/// from <see cref="Web"/> / <see cref="App"/>; a mismatch returns 403
/// <c>AUTH_WRONG_SURFACE_*</c>.
/// </summary>
public enum SignInAudience
{
    /// <summary>The visitor Website (Blazor SSR). Default — least privileged.</summary>
    Web = 0,

    /// <summary>The Control Panel (Blazor Server) — staff / admin only.</summary>
    Cp = 1,

    /// <summary>The Flutter mobile app — visitors only, same rule as Web.</summary>
    App = 2,
}

/// <summary>The body of <c>POST /api/v1/auth/sign-in</c> (SIMF-API-001 section 12.4).</summary>
public sealed class SignInRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// The surface this sign-in attempt came from (P2). Defaults to
    /// <see cref="SignInAudience.Web"/> — the least-privileged surface — so
    /// any caller that forgets to set it falls into the visitor bucket.
    /// </summary>
    public SignInAudience Audience { get; set; } = SignInAudience.Web;
}

/// <summary>
/// The result of the password step.
/// <list type="bullet">
///   <item>When the account has <c>TwoFactorEnabled = true</c>, exactly one of
///     <see cref="MfaToken"/> (Control Panel users — TOTP) or
///     <see cref="OtpToken"/> (visitors — email OTP) is set, and
///     <see cref="MfaRequired"/> is <c>true</c>.</item>
///   <item>When the account has <c>TwoFactorEnabled = false</c>, neither token
///     is set, <see cref="MfaRequired"/> is <c>false</c> and <see cref="Tokens"/>
///     carries the issued tokens directly — sign-in is complete (myComment #34,
///     decision D-033).</item>
/// </list>
/// </summary>
public sealed record SignInResponse(
    bool MfaRequired,
    string? MfaToken,
    string? OtpToken,
    AuthTokens? Tokens = null);

/// <summary>The token payload returned once a sign-in is fully completed.</summary>
public sealed record AuthTokens(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int AccessTokenExpiresInSeconds,
    AuthUser User);

/// <summary>The signed-in user, as carried in <see cref="AuthTokens"/>.</summary>
public sealed record AuthUser(Guid Id, string Email, string DisplayName);
