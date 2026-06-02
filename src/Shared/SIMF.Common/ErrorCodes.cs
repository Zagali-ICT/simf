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

    // Issue-1 — RBAC role assignment to an existing admin user.
    public const string AdminRolesTargetNotAdmin = "ADMIN_ROLES_TARGET_NOT_ADMIN";
    public const string AdminCannotRemoveLastAdministrator = "ADMIN_CANNOT_REMOVE_LAST_ADMINISTRATOR";

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

    // Speaker presentations (P2.3 / D-228 — FR-407).
    public const string SpeakerPresentationInvalid = "SPEAKER_PRESENTATION_INVALID";
    public const string SpeakerPresentationNotFound = "SPEAKER_PRESENTATION_NOT_FOUND";

    // System configuration settings (P2.4 / D-229 — FDS-012 §5.5).
    public const string SystemSettingInvalid = "SYSTEM_SETTING_INVALID";
    public const string SystemSettingNotFound = "SYSTEM_SETTING_NOT_FOUND";
    public const string SystemSettingKeyDuplicate = "SYSTEM_SETTING_KEY_DUPLICATE";

    // Venue map nodes (P2.5 / D-230 — FR-605, FDS-006 §5.3).
    public const string VenueMapNodeInvalid = "VENUE_MAP_NODE_INVALID";
    public const string VenueMapNodeNotFound = "VENUE_MAP_NODE_NOT_FOUND";

    // Sponsors (D-199 — event sponsors, Mockup page 23).
    public const string SponsorInvalid = "SPONSOR_INVALID";
    public const string SponsorNotFound = "SPONSOR_NOT_FOUND";
    public const string SponsorDuplicate = "SPONSOR_DUPLICATE";

    // Media partners (D-199 — Mockup page 31 media partners).
    public const string MediaPartnerNameDuplicate = "MEDIA_PARTNER_NAME_DUPLICATE";

    // Booths (D-199 — Exhibition module, Mockup page 22 + 2D venue map).
    public const string BoothInvalid = "BOOTH_INVALID";
    public const string BoothNotFound = "BOOTH_NOT_FOUND";
    public const string BoothCodeDuplicate = "BOOTH_CODE_DUPLICATE";

    // News (D-199 — PR / marketing news, Mockup. Promoted from
    // AdminNewsService module-local consts; string values are the wire
    // contract and must stay verbatim).
    public const string NewsInvalid = "NEWS_INVALID";
    public const string NewsNotFound = "NEWS_NOT_FOUND";
    public const string NewsTitleDuplicate = "NEWS_TITLE_DUPLICATE";

    // FAQ (P2.1 / D-211).
    public const string FaqInvalid = "FAQ_INVALID";
    public const string FaqGroupNotFound = "FAQ_GROUP_NOT_FOUND";
    public const string FaqEntryNotFound = "FAQ_ENTRY_NOT_FOUND";

    // Media (D-199 — media gallery, Mockup page 30. Promoted from the
    // module-local MediaErrorCodes; string values are the wire contract).
    public const string MediaNotFound = "media_not_found";
    public const string MediaInvalid = "media_invalid";

    // Archive (D-199 — past editions, Mockup screen 24. Promoted from
    // AdminArchiveService module-local consts; string values are the wire
    // contract).
    public const string ArchiveEditionNotFound = "archive_edition_not_found";
    public const string ArchiveEditionInvalid = "archive_edition_invalid";
    public const string ArchiveEditionYearDuplicate = "archive_edition_year_duplicate";

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

    // Audience comments (D-199, Mockup page 28 — تعليقات الجمهور;
    // distinct from SessionQuestion — public feed + admin moderation).
    public const string SessionCommentInvalid = "SESSION_COMMENT_INVALID";
    public const string SessionCommentNotFound = "SESSION_COMMENT_NOT_FOUND";
    public const string SessionNotOpenForComments = "SESSION_NOT_OPEN_FOR_COMMENTS";

    // Venue self-assert (D-171, gap doc G7 — PDF §2.10; G-OI-2 resolved
    // to the self-assert toggle as input source).
    public const string NotAtVenue = "NOT_AT_VENUE";

    // Delegations + MeetingRequests (D-174, gap doc G11 — Mockup pages 21 + 27).
    public const string DelegationInvalid = "DELEGATION_INVALID";
    public const string DelegationNotFound = "DELEGATION_NOT_FOUND";
    public const string MeetingRequestInvalid = "MEETING_REQUEST_INVALID";
    public const string MeetingRequestNotFound = "MEETING_REQUEST_NOT_FOUND";
    public const string MeetingRequestSessionNotFound = "MEETING_REQUEST_SESSION_NOT_FOUND";
    public const string MeetingRequestStatusInvalid = "MEETING_REQUEST_STATUS_INVALID";

    // Seat reservations (D-175, gap doc G11 — Mockup page 7).
    public const string SeatLayoutInvalid = "SEAT_LAYOUT_INVALID";
    public const string SeatLayoutMissing = "SEAT_LAYOUT_MISSING";
    public const string SeatOutOfBounds = "SEAT_OUT_OF_BOUNDS";
    public const string SeatAlreadyReserved = "SEAT_ALREADY_RESERVED";
    public const string SeatAlreadyOwnedBySession = "SEAT_ALREADY_OWNED_BY_SESSION";
    public const string SeatReservationNotFound = "SEAT_RESERVATION_NOT_FOUND";
    public const string SeatCapacityExceeded = "SEAT_CAPACITY_EXCEEDED";
    public const string SeatSessionFull = "SEAT_SESSION_FULL";

    // Booking approval workflow (P2.2 / D-227 — FDS-005 §5).
    public const string BookingOverlap = "BOOKING_OVERLAP";
    public const string BookingNotFound = "BOOKING_NOT_FOUND";
    public const string BookingNotPending = "BOOKING_NOT_PENDING";
    public const string BookingRejectionReasonRequired = "BOOKING_REJECTION_REASON_REQUIRED";
    public const string BookingSessionStarted = "BOOKING_SESSION_STARTED";

    // Centralised AI module (D-176, gap doc G12).
    public const string AiPromptInvalid = "AI_PROMPT_INVALID";
    public const string AiPromptNotFound = "AI_PROMPT_NOT_FOUND";
    public const string AiPromptKeyDuplicate = "AI_PROMPT_KEY_DUPLICATE";
    public const string AiProviderNotConfigured = "AI_PROVIDER_NOT_CONFIGURED";
    public const string AiProviderFailed = "AI_PROVIDER_FAILED";
    public const string AiInputInvalid = "AI_INPUT_INVALID";
    public const string AiFeatureDisabled = "AI_FEATURE_DISABLED";

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

    // Companies + exhibitor/sponsor account provisioning (D-202 — D-199 #3).
    public const string CompanyInvalid = "COMPANY_INVALID";
    public const string CompanyNotFound = "COMPANY_NOT_FOUND";
    public const string CompanyInactive = "COMPANY_INACTIVE";
    public const string CompanyAccountInvalid = "COMPANY_ACCOUNT_INVALID";

    // Organisations (B3 / D-220 — Saudi-companies lookup, government Excel
    // bulk-import; the visitor الجهة picker reads from this table).
    public const string OrganisationInvalid = "ORGANISATION_INVALID";
    public const string OrganisationNotFound = "ORGANISATION_NOT_FOUND";
    public const string OrganisationImportFailed = "ORGANISATION_IMPORT_FAILED";

    // Networking connections (B6 / D-224 — visitor-to-visitor request/accept).
    public const string ConnectionInvalid = "CONNECTION_INVALID";
    public const string ConnectionNotFound = "CONNECTION_NOT_FOUND";
    public const string ConnectionAlreadyExists = "CONNECTION_ALREADY_EXISTS";
    public const string ConnectionSelf = "CONNECTION_SELF";
    public const string ConnectionTargetNotFound = "CONNECTION_TARGET_NOT_FOUND";

    // Session categories (B9b / D-226 — dynamic lookup, FDS-004 §5.4).
    public const string SessionCategoryInvalid = "SESSION_CATEGORY_INVALID";
    public const string SessionCategoryNotFound = "SESSION_CATEGORY_NOT_FOUND";
}
