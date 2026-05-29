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
    /// <summary>H11 — D-066: the new password failed an ASP.NET Identity
    /// policy rule (length, digit, …). Used in the audit log only — the
    /// response shape still raises <see cref="DataValidationException"/>
    /// with per-error details on <c>newPassword</c>.</summary>
    public const string AuthPasswordPolicy = "AUTH_PASSWORD_POLICY";
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
    // D-164 (gap doc G2) — bulk-action invalid (empty array, etc.).
    public const string AdminBulkActionInvalid = "ADMIN_BULK_ACTION_INVALID";

    // P7c — ProfileTypes lookup validation
    public const string AdminProfileTypeInvalid = "ADMIN_PROFILE_TYPE_INVALID";

    // D-115 — ProfileTypes CRUD (admin lookup management)
    public const string ProfileTypeNotFound = "PROFILE_TYPE_NOT_FOUND";
    public const string ProfileTypeInUse = "PROFILE_TYPE_IN_USE";
    public const string ProfileTypeInvalidUserType = "PROFILE_TYPE_INVALID_USER_TYPE";
    public const string ProfileTypeNameTaken = "PROFILE_TYPE_NAME_TAKEN";

    // User profile — ID document image (D-046 b; P8 kept the wire codes
    // VISITOR_ID_IMAGE_* unchanged so any consumer already mapping them
    // does not break; only the C# symbol name still says "Visitor" today
    // and stays for one release window).
    public const string VisitorIdImageMissing = "VISITOR_ID_IMAGE_MISSING";
    public const string VisitorIdImageTooLarge = "VISITOR_ID_IMAGE_TOO_LARGE";
    public const string VisitorIdImageMimeUnsupported = "VISITOR_ID_IMAGE_MIME_UNSUPPORTED";
    public const string VisitorIdImageNotFound = "VISITOR_ID_IMAGE_NOT_FOUND";

    // User profile — nationality (P8 renamed from VISITOR_NATIONALITY_UNKNOWN
    // so the wire code matches the new domain vocabulary).
    public const string ProfileNationalityUnknown = "PROFILE_NATIONALITY_UNKNOWN";

    // Interests (P9 — D-050; الاهتمامات)
    public const string InterestInvalid = "INTEREST_INVALID";
    public const string InterestNotFound = "INTEREST_NOT_FOUND";
    public const string InterestNameDuplicate = "INTEREST_NAME_DUPLICATE";

    // Roles (D-134 Sprint A — admin CRUD over existing SimfRole +
    // RolePermission + Permission entities — no schema change).
    public const string RoleInvalid = "ROLE_INVALID";
    public const string RoleNotFound = "ROLE_NOT_FOUND";
    public const string RoleNameDuplicate = "ROLE_NAME_DUPLICATE";
    public const string RoleIsBaseline = "ROLE_IS_BASELINE";
    public const string RoleInUse = "ROLE_IN_USE";

    // Themes (D-134 Sprint B — programme themes / pillars, SIMF-FDS-004 §5.1).
    public const string ThemeInvalid = "THEME_INVALID";
    public const string ThemeNotFound = "THEME_NOT_FOUND";
    public const string ThemeCodeDuplicate = "THEME_CODE_DUPLICATE";
    public const string ThemeInUse = "THEME_IN_USE";

    // Halls (D-134 Sprint B — venue halls, SIMF-FDS-004 §5.2).
    public const string HallInvalid = "HALL_INVALID";
    public const string HallNotFound = "HALL_NOT_FOUND";
    public const string HallCodeDuplicate = "HALL_CODE_DUPLICATE";
    public const string HallInUse = "HALL_IN_USE";

    // Countries (D-151 — admin-managed country lookup).
    public const string CountryInvalid = "COUNTRY_INVALID";
    public const string CountryNotFound = "COUNTRY_NOT_FOUND";
    public const string CountryCodeDuplicate = "COUNTRY_CODE_DUPLICATE";
    public const string CountryIdDuplicate = "COUNTRY_ID_DUPLICATE";
    public const string CountryInUse = "COUNTRY_IN_USE";

    // Speakers (D-151 — programme speakers, SIMF-DAT-001 §5.4).
    public const string SpeakerInvalid = "SPEAKER_INVALID";
    public const string SpeakerNotFound = "SPEAKER_NOT_FOUND";
    public const string SpeakerCodeDuplicate = "SPEAKER_CODE_DUPLICATE";
    public const string SpeakerInUse = "SPEAKER_IN_USE";

    // Operations toggles (D-166, gap doc G4 — registration gate +
    // archive visibility singletons).
    public const string RegistrationClosed = "REGISTRATION_CLOSED";

    // Session questions + moderator grants (D-169, gap doc G6 —
    // PDF §2.7.2, distinct from MobileAppRole.Moderator).
    public const string SessionQuestionInvalid = "SESSION_QUESTION_INVALID";
    public const string SessionQuestionNotFound = "SESSION_QUESTION_NOT_FOUND";
    public const string SessionNotLiveForQuestions = "SESSION_NOT_LIVE_FOR_QUESTIONS";
    public const string SessionModeratorNotAssigned = "SESSION_MODERATOR_NOT_ASSIGNED";
    public const string SessionModeratorAlreadyAssigned = "SESSION_MODERATOR_ALREADY_ASSIGNED";

    // Venue self-assert (D-171, gap doc G7 — PDF §2.10; G-OI-2 resolved
    // to the self-assert toggle as input source).
    public const string NotAtVenue = "NOT_AT_VENUE";

    // CMS: ContentBlock + Banner (D-173, gap doc G8 — PDF §1, §2.1).
    public const string ContentBlockInvalid = "CONTENT_BLOCK_INVALID";
    public const string ContentBlockNotFound = "CONTENT_BLOCK_NOT_FOUND";
    public const string ContentBlockKeyDuplicate = "CONTENT_BLOCK_KEY_DUPLICATE";
    public const string BannerInvalid = "BANNER_INVALID";
    public const string BannerNotFound = "BANNER_NOT_FOUND";
    public const string BannerInvalidTimeWindow = "BANNER_INVALID_TIME_WINDOW";

    // Device keys / biometric sign-in (D-172, gap doc G10 — PDF §2.5).
    public const string DeviceKeyInvalid = "DEVICE_KEY_INVALID";
    public const string DeviceKeyNotFound = "DEVICE_KEY_NOT_FOUND";
    public const string DeviceKeyAlgorithmUnsupported = "DEVICE_KEY_ALGORITHM_UNSUPPORTED";
    public const string DeviceKeyRevoked = "DEVICE_KEY_REVOKED";
    public const string DeviceKeyChallengeInvalid = "DEVICE_KEY_CHALLENGE_INVALID";
    public const string DeviceKeySignatureInvalid = "DEVICE_KEY_SIGNATURE_INVALID";
    public const string DeviceKeyOwnerUnavailable = "DEVICE_KEY_OWNER_UNAVAILABLE";

    // Invitations + VIP notify (D-168, gap doc G5 — public-relations
    // module, PDF §2.7.3).
    public const string InvitationInvalid = "INVITATION_INVALID";
    public const string InvitationNotFound = "INVITATION_NOT_FOUND";
    public const string InvitationTargetNotFound = "INVITATION_TARGET_NOT_FOUND";
    public const string InvitationStateInvalid = "INVITATION_STATE_INVALID";
    public const string VipNotifyEmpty = "VIP_NOTIFY_EMPTY";
    public const string VipNotifyTooLarge = "VIP_NOTIFY_TOO_LARGE";

    // Sessions (D-165, gap doc G3 — programme sessions tied to a Hall +
    // M-to-M Speakers + M-to-M Themes).
    public const string SessionInvalid = "SESSION_INVALID";
    public const string SessionNotFound = "SESSION_NOT_FOUND";
    public const string SessionCodeDuplicate = "SESSION_CODE_DUPLICATE";
    public const string SessionInvalidTimeWindow = "SESSION_INVALID_TIME_WINDOW";
    public const string SessionHallNotFound = "SESSION_HALL_NOT_FOUND";
    public const string SessionSpeakerNotFound = "SESSION_SPEAKER_NOT_FOUND";
    public const string SessionThemeNotFound = "SESSION_THEME_NOT_FOUND";

    // Gates (D-148 — Gate Module, SIMF-FDS-003 §5.6, SIMF-API-GATES-001).
    public const string GateInvalid = "GATE_INVALID";
    public const string GateNotFound = "GATE_NOT_FOUND";
    public const string GateCodeDuplicate = "GATE_CODE_DUPLICATE";
    public const string GateInactive = "GATE_INACTIVE";
    public const string GateOperatorNotAssigned = "GATE_OPERATOR_NOT_ASSIGNED";
    public const string GateAssignmentInvalid = "GATE_ASSIGNMENT_INVALID";
    public const string GateProfileTypeInvalid = "GATE_PROFILE_TYPE_INVALID";
    public const string IdempotencyKeyConflict = "IDEMPOTENCY_KEY_CONFLICT";
    public const string GateFailureCircuitOpen = "GATE_FAILURE_CIRCUIT_OPEN";
}
