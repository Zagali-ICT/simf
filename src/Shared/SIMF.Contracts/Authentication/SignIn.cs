namespace SIMF.Contracts.Authentication;

/// <summary>The body of <c>POST /api/v1/auth/sign-in</c> (SIMF-API-001 section 12.4).</summary>
public sealed class SignInRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// The result of the password step. SIMF always requires a second factor, so
/// one of <see cref="MfaToken"/> (Control Panel users — TOTP) or
/// <see cref="OtpToken"/> (visitors — email OTP) is set.
/// </summary>
public sealed record SignInResponse(bool MfaRequired, string? MfaToken, string? OtpToken);

/// <summary>The token payload returned once a sign-in is fully completed.</summary>
public sealed record AuthTokens(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int AccessTokenExpiresInSeconds,
    AuthUser User);

/// <summary>The signed-in user, as carried in <see cref="AuthTokens"/>.</summary>
public sealed record AuthUser(Guid Id, string Email, string DisplayName);
