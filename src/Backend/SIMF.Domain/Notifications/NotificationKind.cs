namespace SIMF.Domain.Notifications;

/// <summary>
/// The stable kind of an in-app notification (P12 — D-053). Persisted
/// as the enum name string (e.g. <c>"AccountApproved"</c>) via the EF
/// value converter on <c>NotificationConfiguration</c>, so a renamed
/// case requires a data migration.
///
/// <para>D-108: replaces the prior magic-string <c>Kind</c> column +
/// the <c>NotificationKinds</c> constants class. A typo at one
/// dispatch site is now a compiler error instead of a silent
/// mismatch between the writer and the bell-UI filter.</para>
/// </summary>
public enum NotificationKind
{
    /// <summary>
    /// Dispatched by <c>RegistrationService.SignUpAsync</c> after the
    /// initial verification email is queued (D-099).
    /// </summary>
    CredentialEmailVerificationSent = 0,

    /// <summary>
    /// Dispatched by <c>RegistrationService.ResendCodeAsync</c> after a
    /// re-issued verification email is queued (D-099).
    /// </summary>
    CredentialEmailVerificationResent = 1,

    /// <summary>
    /// Dispatched by <c>SignInService.SignInAsync</c> (email-OTP branch
    /// only) after the sign-in OTP email is queued (D-099).
    /// </summary>
    CredentialSignInOtpSent = 2,

    /// <summary>
    /// Dispatched by <c>PasswordService.ForgotPasswordAsync</c> after
    /// the reset email is queued (D-099).
    /// </summary>
    CredentialPasswordResetRequested = 3,

    /// <summary>
    /// Dispatched by <c>UserProfileService</c> after the first profile
    /// save auto-transitions the user to PendingApproval (H2 — D-057).
    /// </summary>
    AccountProfileSubmitted = 10,

    /// <summary>
    /// Dispatched by <c>UserProfileService</c> to every Administrator
    /// when a new visitor becomes pending approval (P13 — D-054).
    /// </summary>
    AdminPendingVisitor = 11,

    /// <summary>
    /// Dispatched by <c>AdminAccountService.ApproveAsync</c> when an
    /// account is approved and the QR id is minted (P13 — D-054).
    /// </summary>
    AccountApproved = 12,

    /// <summary>
    /// Dispatched by <c>AdminAccountService.RejectAsync</c> when an
    /// account is rejected (P13 — D-054).
    /// </summary>
    AccountRejected = 13,

    /// <summary>
    /// Dispatched by <c>AdminAccountService.ResetTwoFactorAsync</c>
    /// when an administrator clears the subject's 2FA (P13 — D-054).
    /// </summary>
    AccountTwoFactorReset = 14,
}
