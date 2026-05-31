using SIMF.Common.Enums;

namespace SIMF.Contracts.Authentication;

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
///   <item>D-206: when a Control Panel account must change a forced
///     (seeded/admin-rotated) password, <see cref="PasswordChangeToken"/> is set
///     and every other field is null/false. The caller collects a new password
///     and completes the change at <c>POST /auth/complete-password-change</c>;
///     no session is minted until that succeeds and the user signs in again.
///     For non-Control-Panel audiences the forced-change case still returns the
///     <c>AUTH_PASSWORD_CHANGE_REQUIRED</c> 403 unchanged.</item>
/// </list>
/// </summary>
public sealed record SignInResponse(
    bool MfaRequired,
    string? MfaToken,
    string? OtpToken,
    AuthTokens? Tokens = null,
    AccountStateInfo? AccountState = null,
    string? PasswordChangeToken = null);

/// <summary>
/// Carries the user's account state on a sign-in response when the
/// account is **not** Approved (P10 — D-051). Null on an Approved
/// sign-in. The front-end branches on <see cref="State"/> to route the
/// user to the pending / rejected state-banner page (P11). The
/// <see cref="RejectionReason"/> + <see cref="RejectionReasonArabic"/>
/// pair is populated only when <see cref="State"/> is
/// <c>"Rejected"</c>.
/// </summary>
public sealed record AccountStateInfo(
    string State,
    string? RejectionReason,
    string? RejectionReasonArabic,
    DateTimeOffset? StateChangedAt);

/// <summary>The token payload returned once a sign-in is fully completed.</summary>
public sealed record AuthTokens(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int AccessTokenExpiresInSeconds,
    AuthUser User);

/// <summary>The signed-in user, as carried in <see cref="AuthTokens"/>.</summary>
public sealed record AuthUser(Guid Id, string Email, string DisplayName);
