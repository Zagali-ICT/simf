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
    public const string SuperAdminSeeded = "Admin.SuperAdminSeeded";
}
