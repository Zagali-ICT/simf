namespace SIMF.Contracts.Authentication;

/// <summary>
/// The body of <c>POST /api/v1/auth/verify-totp</c> — the Control Panel
/// second-factor step (SIMF-API-001 section 12.4).
/// </summary>
public sealed class VerifyTotpRequest
{
    public string MfaToken { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}
