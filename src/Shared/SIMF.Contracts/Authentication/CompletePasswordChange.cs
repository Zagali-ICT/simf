namespace SIMF.Contracts.Authentication;

/// <summary>
/// The body of <c>POST /api/v1/app/auth/complete-password-change</c> — a
/// Control Panel operator setting a new password against the single-use
/// password-change ticket the sign-in step issued for a forced-change
/// credential. The ticket proves the current password was already verified at
/// sign-in, so the current password is not re-collected.
/// </summary>
public sealed class CompletePasswordChangeRequest
{
    /// <summary>The single-use ticket returned by the sign-in password step.</summary>
    public string PasswordChangeToken { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>The result of a completed forced password change (D-206).</summary>
public sealed record CompletePasswordChangeResponse(bool PasswordChanged);
