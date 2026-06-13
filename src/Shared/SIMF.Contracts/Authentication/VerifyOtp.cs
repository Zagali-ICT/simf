namespace SIMF.Contracts.Authentication;

/// <summary>
/// The body of <c>POST /api/v1/app/auth/verify-otp</c> — the visitor email-OTP
/// second-factor step (SIMF-API-001 Amendment A.1).
/// </summary>
public sealed class VerifyOtpRequest
{
    public string OtpToken { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}
