using SIMF.Common.Enums;

namespace SIMF.Contracts.Authentication;

/// <summary>The body of <c>POST /api/v1/app/auth/sign-in</c> (SIMF-API-001 section 12.4).</summary>
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
/// <remarks>A7-31 (NCA): <see cref="PreviousSignInAtUtc"/> carries the time of the
/// account's prior successful sign-in (null on the very first one, and on token
/// refresh) so the client can show a "last signed in …" notice. Additive trailing
/// field — the mobile/web wire contract stays backward-compatible.</remarks>
public sealed record AuthTokens(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int AccessTokenExpiresInSeconds,
    AuthUser User,
    DateTimeOffset? PreviousSignInAtUtc = null);

/// <summary>The signed-in user, as carried in <see cref="AuthTokens"/>.</summary>
public sealed record AuthUser(Guid Id, string Email, string DisplayName);

/// <summary>
/// The signed-in user as the Flutter app decodes it from
/// <c>GET /api/v1/app/users/me</c> (SIMF-MOB-API-001 §5.1) — the same wire
/// shape the app's <c>CurrentUserDto</c> consumes. Built additively for the
/// Registration-Status screen (Page 011) so the app can poll the approval
/// state; available to any signed-in account, including not-yet-approved ones
/// (D-249).
/// <list type="bullet">
///   <item><see cref="AppRole"/> is the resolved mobile app-role name —
///     <c>"Visitor"</c>, <c>"Staff"</c> or <c>"Moderator"</c> — matching the
///     app's <c>AppRole</c> wire names (the string form is used so the int
///     drift between the two enums never matters).</item>
///   <item><see cref="RegistrationStatus"/> is the three-value app vocabulary
///     <c>"Pending"</c> / <c>"Approved"</c> / <c>"Rejected"</c>, collapsed from
///     the six-value <see cref="SIMF.Common.Enums.AccountState"/>.</item>
///   <item><see cref="PreferredLanguage"/> is the IETF short tag (<c>"ar"</c> /
///     <c>"en"</c>); the Identity row carries no per-user language today, so it
///     is the primary-language default.</item>
/// </list>
/// </summary>
public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string AppRole,
    string PreferredLanguage,
    string RegistrationStatus,
    string? AvatarUrl,
    // D-374 — server-computed profile completeness so the app can force the
    // add-profile stage right after ANY login path (names + ≥1 interest +
    // the C7 male-photo rule), without a separate profile probe. Additive
    // wire field (append-only contract).
    bool ProfileComplete = false);
