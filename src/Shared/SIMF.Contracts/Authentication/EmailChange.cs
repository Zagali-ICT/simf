namespace SIMF.Contracts.Authentication;

/// <summary>
/// #24 — the body of <c>POST /api/v1/app/auth/change-email/send-otp</c>: a
/// signed-in user asks to move their login email to <see cref="NewEmail"/>. The
/// server checks the address is free and emails a one-time code TO that new
/// address, so the change only completes once the user proves they control it.
/// </summary>
public sealed class SendEmailChangeOtpRequest
{
    public string NewEmail { get; set; } = string.Empty;
}

/// <summary>
/// #24 — the result of requesting an email-change code: the masked address the
/// code was emailed to (for the "we sent a code to t***@x" line), how long the
/// code stays valid, and the resend cooldown for the client's resend button.
/// Never carries the plaintext code.
/// </summary>
public sealed record SendEmailChangeOtpResponse(
    string MaskedNewEmail,
    int ExpiresInSeconds,
    int ResendCooldownSeconds);

/// <summary>
/// #24 — the body of <c>POST /api/v1/app/auth/change-email/confirm</c>: the
/// signed-in user submits the code emailed to <see cref="NewEmail"/> together with
/// their <see cref="CurrentPassword"/> to complete the change. The password
/// re-authentication proves the request comes from the account owner (not just a
/// held session), so a stolen access token alone cannot seize the account. On
/// success the login email moves, the security stamp is rolled and every session
/// is revoked, so the app must sign the user out and re-authenticate.
/// </summary>
public sealed class ConfirmEmailChangeRequest
{
    public string NewEmail { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    /// <summary>The account's current password — re-authentication that proves the
    /// change was requested by the owner, not merely by a held session.</summary>
    public string CurrentPassword { get; set; } = string.Empty;
}

/// <summary>#24 — the result of a completed email change.</summary>
public sealed record ConfirmEmailChangeResponse(bool EmailChanged);
