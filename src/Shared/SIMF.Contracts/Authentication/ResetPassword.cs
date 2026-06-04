namespace SIMF.Contracts.Authentication;

/// <summary>The body of <c>POST /api/v1/auth/reset-password</c> (SIMF-API-001 section 12.7).</summary>
public sealed class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>The result of a completed password reset (SIMF-API-001 section 12.7).</summary>
public sealed record ResetPasswordResponse(bool PasswordReset);
