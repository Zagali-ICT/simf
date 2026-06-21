namespace SIMF.Contracts.Authentication;

/// <summary>The body of <c>POST /api/v1/app/auth/resend-otp</c> — re-issue the
/// emailed sign-in one-time code for an in-progress visitor 2FA session, keyed
/// by the OTP ticket (no password needed; the ticket already proved it).</summary>
public sealed class ResendOtpRequest
{
    public string OtpToken { get; set; } = string.Empty;
}

/// <summary>The data returned by a successful OTP resend. The same ticket stays
/// valid (its window is refreshed), so the client keeps its <c>otpToken</c> and
/// just restarts the resend cooldown.</summary>
public sealed record ResendOtpResponse(int CooldownSeconds);
