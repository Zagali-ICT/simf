namespace SIMF.Domain.Notifications;

/// <summary>
/// Stable identifiers for the <see cref="Notification.Kind"/> column.
/// Persisted in the DB and read by the bell UI, lifecycle tests, and any
/// future analytics — a typo at one dispatch site would silently miss
/// the relationship with the others. Per CLAUDE.md §2: avoid magic strings.
/// </summary>
public static class NotificationKinds
{
    /// <summary>
    /// Dispatched by <c>PasswordService.ForgotPasswordAsync</c> after the
    /// reset email is queued (D-099).
    /// </summary>
    public const string CredentialPasswordResetRequested = "Credential.PasswordResetRequested";

    /// <summary>
    /// Dispatched by <c>RegistrationService.SignUpAsync</c> after the
    /// initial verification email is queued (D-099).
    /// </summary>
    public const string CredentialEmailVerificationSent = "Credential.EmailVerificationSent";

    /// <summary>
    /// Dispatched by <c>RegistrationService.ResendCodeAsync</c> after a
    /// re-issued verification email is queued (D-099).
    /// </summary>
    public const string CredentialEmailVerificationResent = "Credential.EmailVerificationResent";

    /// <summary>
    /// Dispatched by <c>SignInService.SignInAsync</c> (email-OTP branch
    /// only — the TOTP branch returns earlier with no email) after the
    /// sign-in OTP email is queued (D-099).
    /// </summary>
    public const string CredentialSignInOtpSent = "Credential.SignInOtpSent";
}
