namespace SIMF.Contracts.Authentication;

/// <summary>
/// The body of <c>POST /api/v1/app/auth/totp/setup</c>. Begins authenticator-app
/// enrolment for the signed-in account; returns the secret, the
/// <c>otpauth://</c> URI to scan with the authenticator and a server-rendered
/// SVG QR code.
/// </summary>
public sealed record TotpSetupResponse(
    string Secret,
    string OtpAuthUri,
    string QrCodeSvg);

/// <summary>The body of <c>POST /api/v1/app/auth/totp/confirm</c>.</summary>
public sealed class TotpConfirmRequest
{
    /// <summary>The six-digit code from the authenticator app.</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>The body of a confirmed TOTP enrolment.</summary>
/// <param name="RecoveryCodes">
/// The freshly minted single-use recovery codes — shown plaintext exactly
/// once (decision D-040). The user must save them now; the API never returns
/// them again, only counts them on the profile.
/// </param>
public sealed record TotpConfirmResponse(
    bool TwoFactorEnabled,
    IReadOnlyList<string> RecoveryCodes);

/// <summary>D-102: the body of <c>POST /api/v1/app/auth/totp/pairing/verify</c>.</summary>
public sealed record TotpPairingVerifyResponse(bool Valid);

/// <summary>The body of <c>POST /api/v1/app/auth/totp/disable</c>.</summary>
public sealed class TotpDisableRequest
{
    /// <summary>The six-digit code from the authenticator app.</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>The body of a disabled TOTP.</summary>
public sealed record TotpDisableResponse(bool TwoFactorEnabled);

/// <summary>The body of <c>POST /api/v1/app/auth/verify-recovery-code</c>.</summary>
public sealed class VerifyRecoveryCodeRequest
{
    /// <summary>The same MFA token issued by the sign-in step that <c>verify-totp</c> uses.</summary>
    public string MfaToken { get; set; } = string.Empty;

    /// <summary>One of the user's single-use recovery codes (with or without the hyphen).</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>The body of <c>POST /api/v1/app/account/recovery-codes/regenerate</c>.</summary>
public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);

/// <summary>The body of <c>GET /api/v1/app/account/profile</c>.</summary>
/// <param name="AvatarUrl">
/// The Control-Panel-relative URL the browser should use to fetch the avatar
/// (e.g. <c>/account/api/avatar/{userId}?v=ticks</c>), or <c>null</c> when no
/// avatar is set. The <c>?v=…</c> cache-buster is the user's
/// <c>UpdatedAt</c> ticks so an avatar replacement defeats browser caches.
/// </param>
/// <param name="RecoveryCodesRemaining">
/// The number of single-use recovery codes the user still has available.
/// Zero either means the user never generated codes (legacy 2FA users) or
/// they have used them all — in both cases the profile UI nudges them to
/// regenerate (D-040).
/// </param>
public sealed record ProfileResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    bool TwoFactorEnabled,
    int RecoveryCodesRemaining,
    IReadOnlyList<string> Roles);

/// <summary>The body of <c>POST /api/v1/app/account/avatar</c> and
/// <c>DELETE /api/v1/app/account/avatar</c>.</summary>
public sealed record AvatarResponse(string? AvatarUrl);
