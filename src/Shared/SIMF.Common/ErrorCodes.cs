namespace SIMF.Common;

/// <summary>
/// The stable SIMF API error codes (SIMF-API-001 section 7, section 12.6 and
/// Amendment A). Code strings are defined here once and never written as
/// literals elsewhere.
/// </summary>
public static class ErrorCodes
{
    // General
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
    public const string InternalError = "INTERNAL_ERROR";
    public const string NotFound = "NOT_FOUND";

    // Authentication (SIMF-API-001 section 12.6 and Amendment A)
    public const string AuthEmailAlreadyRegistered = "AUTH_EMAIL_ALREADY_REGISTERED";
    public const string AuthAccountNotFound = "AUTH_ACCOUNT_NOT_FOUND";
    public const string AuthCodeInvalid = "AUTH_CODE_INVALID";
    public const string AuthCodeExpired = "AUTH_CODE_EXPIRED";
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AuthEmailNotVerified = "AUTH_EMAIL_NOT_VERIFIED";
    public const string AuthAccountNotApproved = "AUTH_ACCOUNT_NOT_APPROVED";
    public const string AuthAccountDisabled = "AUTH_ACCOUNT_DISABLED";
    public const string AuthAccountLocked = "AUTH_ACCOUNT_LOCKED";
    public const string AuthMfaTokenInvalid = "AUTH_MFA_TOKEN_INVALID";
    public const string AuthMfaTokenExpired = "AUTH_MFA_TOKEN_EXPIRED";
    public const string AuthTotpInvalid = "AUTH_TOTP_INVALID";
    public const string AuthOtpInvalid = "AUTH_OTP_INVALID";
    public const string AuthOtpExpired = "AUTH_OTP_EXPIRED";
    public const string AuthOtpTokenInvalid = "AUTH_OTP_TOKEN_INVALID";
    public const string AuthRefreshTokenInvalid = "AUTH_REFRESH_TOKEN_INVALID";
    public const string AuthRefreshTokenExpired = "AUTH_REFRESH_TOKEN_EXPIRED";
    public const string AuthResetCodeInvalid = "AUTH_RESET_CODE_INVALID";
    public const string AuthResetCodeExpired = "AUTH_RESET_CODE_EXPIRED";
    public const string AuthPasswordChangeRequired = "AUTH_PASSWORD_CHANGE_REQUIRED";

    // Sign-in audience gate (P2 — myComment "never any user type other than
    // super admin can access CP, and same for WEB/APP")
    public const string AuthWrongSurfaceCp = "AUTH_WRONG_SURFACE_CP";
    public const string AuthWrongSurfaceWeb = "AUTH_WRONG_SURFACE_WEB";

    // TOTP enrolment (myComment #11)
    public const string TotpEnrolmentNotStarted = "TOTP_ENROLMENT_NOT_STARTED";
    public const string TotpEnrolmentCodeInvalid = "TOTP_ENROLMENT_CODE_INVALID";
    public const string TotpNotEnabled = "TOTP_NOT_ENABLED";
    public const string AuthRecoveryCodeInvalid = "AUTH_RECOVERY_CODE_INVALID";

    // Admin-driven 2FA reset (D-041)
    public const string AdminCannotResetSelf = "ADMIN_CANNOT_RESET_SELF";
    public const string AdminCannotResetAdministrator = "ADMIN_CANNOT_RESET_ADMINISTRATOR";

    // Avatar (myComment #11)
    public const string AvatarFileTooLarge = "AVATAR_FILE_TOO_LARGE";
    public const string AvatarMimeUnsupported = "AVATAR_MIME_UNSUPPORTED";
    public const string AvatarFileMissing = "AVATAR_FILE_MISSING";

    // Admin user-creation (D-042)
    public const string AdminEmailAlreadyRegistered = "ADMIN_EMAIL_ALREADY_REGISTERED";

    // Admin bulk actions (D-044 b)
    public const string AdminUserNotFound = "ADMIN_USER_NOT_FOUND";
    public const string AdminImportEmpty = "ADMIN_IMPORT_EMPTY";

    // Admin approval workflow (P4)
    public const string AdminUserNotPending = "ADMIN_USER_NOT_PENDING";

    // Visitor profile (D-046 b)
    public const string VisitorIdImageMissing = "VISITOR_ID_IMAGE_MISSING";
    public const string VisitorIdImageTooLarge = "VISITOR_ID_IMAGE_TOO_LARGE";
    public const string VisitorIdImageMimeUnsupported = "VISITOR_ID_IMAGE_MIME_UNSUPPORTED";
    public const string VisitorIdImageNotFound = "VISITOR_ID_IMAGE_NOT_FOUND";
    public const string VisitorNationalityUnknown = "VISITOR_NATIONALITY_UNKNOWN";
}
