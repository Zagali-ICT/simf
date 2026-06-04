namespace SIMF.Contracts.Authentication;

/// <summary>
/// The body of <c>POST /api/v1/auth/change-password</c> — for an authenticated
/// user changing their own password (SIMF-API-001 section 12.7).
/// </summary>
public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>The result of a completed password change (SIMF-API-001 section 12.7).</summary>
public sealed record ChangePasswordResponse(bool PasswordChanged);
