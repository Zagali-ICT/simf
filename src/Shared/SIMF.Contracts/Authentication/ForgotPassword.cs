namespace SIMF.Contracts.Authentication;

/// <summary>The body of <c>POST /api/v1/auth/forgot-password</c> (SIMF-API-001 section 12.7).</summary>
public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// The result of a forgot-password request — the code lifetime, a constant
/// returned whether or not the account exists so it never reveals which
/// (SIMF-API-001 section 12.7).
/// </summary>
public sealed record ForgotPasswordResponse(int CodeExpiresInSeconds);
