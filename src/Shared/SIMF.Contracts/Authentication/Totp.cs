namespace SIMF.Contracts.Authentication;

/// <summary>
/// The body of <c>POST /api/v1/auth/totp/setup</c>. Begins authenticator-app
/// enrolment for the signed-in account; returns the secret, the
/// <c>otpauth://</c> URI to scan with the authenticator and a server-rendered
/// SVG QR code.
/// </summary>
public sealed record TotpSetupResponse(
    string Secret,
    string OtpAuthUri,
    string QrCodeSvg);

/// <summary>The body of <c>POST /api/v1/auth/totp/confirm</c>.</summary>
public sealed class TotpConfirmRequest
{
    /// <summary>The six-digit code from the authenticator app.</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>The body of a confirmed TOTP enrolment.</summary>
public sealed record TotpConfirmResponse(bool TwoFactorEnabled);

/// <summary>The body of <c>POST /api/v1/auth/totp/disable</c>.</summary>
public sealed class TotpDisableRequest
{
    /// <summary>The six-digit code from the authenticator app.</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>The body of a disabled TOTP.</summary>
public sealed record TotpDisableResponse(bool TwoFactorEnabled);

/// <summary>The body of <c>GET /api/v1/account/profile</c>.</summary>
public sealed record ProfileResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarDataUri,
    bool TwoFactorEnabled,
    IReadOnlyList<string> Roles);

/// <summary>The body of <c>POST /api/v1/account/avatar</c> and
/// <c>DELETE /api/v1/account/avatar</c>.</summary>
public sealed record AvatarResponse(string? AvatarDataUri);
