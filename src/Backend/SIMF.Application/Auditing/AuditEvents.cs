namespace SIMF.Application.Auditing;

/// <summary>
/// The stable audit event-type names (SIMF-FDS-001 section 9). Names are
/// defined here once and never written as literals elsewhere.
/// </summary>
public static class AuditEvents
{
    public const string SignUpSucceeded = "SignUp.Succeeded";
    public const string SignUpDuplicateEmail = "SignUp.DuplicateEmail";
    public const string EmailVerificationSucceeded = "EmailVerification.Succeeded";
    public const string EmailVerificationCodeIncorrect = "EmailVerification.CodeIncorrect";
    public const string EmailVerificationAttemptCapReached = "EmailVerification.AttemptCapReached";
    public const string EmailVerificationCodeExpired = "EmailVerification.CodeExpired";
    public const string EmailVerificationAccountNotFound = "EmailVerification.AccountNotFound";
    public const string EmailVerificationAccountNotRegistered = "EmailVerification.AccountNotRegistered";
    public const string ResendCodeIssued = "ResendCode.Issued";
    public const string ResendCodeAccountNotFound = "ResendCode.AccountNotFound";
    public const string ResendCodeAccountNotRegistered = "ResendCode.AccountNotRegistered";
    public const string ResendCodeCapReached = "ResendCode.CapReached";
    public const string RateLimitRejected = "RateLimit.Rejected";
    public const string SignInBadCredentials = "SignIn.BadCredentials";
    public const string SignInAccountLockedOut = "SignIn.AccountLockedOut";
    public const string SignInStateBlocked = "SignIn.StateBlocked";
    public const string SignInSecondFactorIssued = "SignIn.SecondFactorIssued";
    public const string SignInSecondFactorFailed = "SignIn.SecondFactorFailed";
    public const string SignInSecondFactorRejected = "SignIn.SecondFactorRejected";
    public const string SignInSucceeded = "SignIn.Succeeded";
    public const string RefreshTokenIssued = "RefreshToken.Issued";
    public const string RefreshTokenRotated = "RefreshToken.Rotated";
    public const string RefreshTokenReused = "RefreshToken.Reused";
    public const string RefreshTokenRejected = "RefreshToken.Rejected";
    public const string AccessTokenRejected = "AccessToken.Rejected";
    public const string SignOutSucceeded = "SignOut.Succeeded";
    public const string ForgotPasswordRequested = "ForgotPassword.Requested";
    public const string PasswordResetCompleted = "PasswordReset.Completed";
    public const string PasswordResetCodeIncorrect = "PasswordReset.CodeIncorrect";
    public const string PasswordResetCodeExpired = "PasswordReset.CodeExpired";
    public const string PasswordResetAttemptCapReached = "PasswordReset.AttemptCapReached";
    public const string PasswordResetAccountNotFound = "PasswordReset.AccountNotFound";
    public const string PasswordChanged = "PasswordChange.Succeeded";
    public const string PasswordChangeFailed = "PasswordChange.Failed";
    public const string SuperAdminSeeded = "Admin.SuperAdminSeeded";

    // TOTP enrolment (myComment #11)
    public const string TotpEnrolmentStarted = "Totp.EnrolmentStarted";
    public const string TotpEnrolmentConfirmed = "Totp.EnrolmentConfirmed";
    public const string TotpEnrolmentFailed = "Totp.EnrolmentFailed";
    public const string TotpDisabled = "Totp.Disabled";
    public const string TotpDisableFailed = "Totp.DisableFailed";
    public const string TotpRecoveryCodesGenerated = "Totp.RecoveryCodesGenerated";
    public const string TotpRecoveryCodesRegenerated = "Totp.RecoveryCodesRegenerated";
    public const string TotpRecoveryCodeUsed = "Totp.RecoveryCodeUsed";
    public const string TotpRecoveryCodeFailed = "Totp.RecoveryCodeFailed";

    // Avatar (myComment #11)
    public const string AvatarUpdated = "Avatar.Updated";
    public const string AvatarRejected = "Avatar.Rejected";

    // Admin-driven user management (myComment #33 — first slice, D-041)
    public const string AdminTwoFactorReset = "Admin.TwoFactorReset";
    public const string AdminTwoFactorResetFailed = "Admin.TwoFactorResetFailed";
}
