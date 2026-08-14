namespace SIMF.Common;

/// <summary>
/// The stable SIMF API error codes. Code strings are defined here once and
/// never written as literals elsewhere.
/// </summary>
public static class ErrorCodes
{
    // General
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
    public const string InternalError = "INTERNAL_ERROR";
    public const string NotFound = "NOT_FOUND";
    public const string Forbidden = "FORBIDDEN";
    // NCA malware scanning — an uploaded file failed the scan.
    public const string UploadMalwareDetected = "UPLOAD_MALWARE_DETECTED";

    // The malware scanner is unavailable and the pipeline is
    // fail-closed, so the upload was rejected rather than stored unscanned.
    public const string UploadScanUnavailable = "UPLOAD_SCAN_UNAVAILABLE";

    // Authentication
    /// <summary>The request carried no usable signed-in identity: either no
    /// bearer token at all, or one whose <c>sub</c> claim is missing or is not
    /// a Guid.</summary>
    public const string AuthNotAuthenticated = "AUTH_NOT_AUTHENTICATED";
    public const string AuthEmailAlreadyRegistered = "AUTH_EMAIL_ALREADY_REGISTERED";
    public const string AuthAccountNotFound = "AUTH_ACCOUNT_NOT_FOUND";
    public const string AuthCodeInvalid = "AUTH_CODE_INVALID";
    public const string AuthCodeExpired = "AUTH_CODE_EXPIRED";
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    /// <summary>The new password failed an ASP.NET Identity
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
    // Badge-QR activation: the resolved account already has a password,
    // so it must use the normal sign-in rather than the set-password flow.
    public const string BadgeAlreadyActivated = "BADGE_ALREADY_ACTIVATED";

    // Sign-in audience gate — no user type other than a super admin may reach
    // the CP, and the same surface separation applies to WEB and APP.
    public const string AuthWrongSurfaceCp = "AUTH_WRONG_SURFACE_CP";
    public const string AuthWrongSurfaceWeb = "AUTH_WRONG_SURFACE_WEB";

    // TOTP enrolment
    public const string TotpEnrolmentNotStarted = "TOTP_ENROLMENT_NOT_STARTED";
    public const string TotpEnrolmentCodeInvalid = "TOTP_ENROLMENT_CODE_INVALID";
    public const string TotpNotEnabled = "TOTP_NOT_ENABLED";
    public const string AuthRecoveryCodeInvalid = "AUTH_RECOVERY_CODE_INVALID";
    /// <summary>The password was correct but the account signing in on the
    /// <c>Cp</c> audience carries no second factor yet, so no access token is
    /// issued: the caller must complete TOTP enrolment first. Distinct from
    /// <see cref="TotpNotEnabled"/> (a TOTP action attempted on an account that never
    /// enrolled) because this one is raised on the sign-in path itself and is the
    /// signal the CP uses to route to its enrolment page rather than to the CP shell.</summary>
    public const string AuthTwoFactorEnrolmentRequired = "AUTH_TWO_FACTOR_ENROLMENT_REQUIRED";

    // Admin-driven 2FA reset
    public const string AdminCannotResetSelf = "ADMIN_CANNOT_RESET_SELF";
    public const string AdminCannotResetAdministrator = "ADMIN_CANNOT_RESET_ADMINISTRATOR";

    // Avatar
    public const string AvatarFileTooLarge = "AVATAR_FILE_TOO_LARGE";
    public const string AvatarMimeUnsupported = "AVATAR_MIME_UNSUPPORTED";
    public const string AvatarFileMissing = "AVATAR_FILE_MISSING";

    // Admin user-creation
    public const string AdminEmailAlreadyRegistered = "ADMIN_EMAIL_ALREADY_REGISTERED";
    // Walk-in registration: the supplied National ID
    // / Iqama / passport already belongs to a profile row (duplicate-identity
    // guard, matched via the deterministic blind-index hash of the identifier).
    public const string DuplicateIdentity = "DUPLICATE_IDENTITY";

    // Admin bulk actions
    public const string AdminUserNotFound = "ADMIN_USER_NOT_FOUND";
    public const string AdminImportEmpty = "ADMIN_IMPORT_EMPTY";

    // Admin approval workflow
    public const string AdminUserNotPending = "ADMIN_USER_NOT_PENDING";
    // Bulk-action invalid (empty array, etc.).
    public const string AdminBulkActionInvalid = "ADMIN_BULK_ACTION_INVALID";

    // ProfileTypes lookup validation
    public const string AdminProfileTypeInvalid = "ADMIN_PROFILE_TYPE_INVALID";

    // RBAC role assignment to an existing admin user.
    public const string AdminRolesTargetNotAdmin = "ADMIN_ROLES_TARGET_NOT_ADMIN";
    public const string AdminCannotRemoveLastAdministrator = "ADMIN_CANNOT_REMOVE_LAST_ADMINISTRATOR";

    // ProfileTypes CRUD (admin lookup management)
    public const string ProfileTypeNotFound = "PROFILE_TYPE_NOT_FOUND";
    public const string ProfileTypeInUse = "PROFILE_TYPE_IN_USE";

    /// <summary>Two profile types were created at the same instant and
    /// both allocated the same badge code. The filtered unique index refused the
    /// loser; retrying succeeds. Translated so a genuine race is a typed 409 the
    /// caller can act on rather than an unhandled 500.</summary>
    public const string ProfileTypeCodeRace = "PROFILE_TYPE_CODE_RACE";
    public const string ProfileTypeInvalidUserType = "PROFILE_TYPE_INVALID_USER_TYPE";
    public const string ProfileTypeNameTaken = "PROFILE_TYPE_NAME_TAKEN";

    // User profile — ID document image. The wire codes VISITOR_ID_IMAGE_* stay
    // unchanged so any consumer already mapping them does not break; only the
    // C# symbol name still says "Visitor" today, and it stays for one release
    // window.
    public const string VisitorIdImageMissing = "VISITOR_ID_IMAGE_MISSING";
    public const string VisitorIdImageTooLarge = "VISITOR_ID_IMAGE_TOO_LARGE";
    public const string VisitorIdImageMimeUnsupported = "VISITOR_ID_IMAGE_MIME_UNSUPPORTED";
    public const string VisitorIdImageNotFound = "VISITOR_ID_IMAGE_NOT_FOUND";
    // The server-side human-face gate on the profile image.
    public const string VisitorIdImageNoFace = "VISITOR_ID_IMAGE_NO_FACE";
    // The face photo (avatar) is mandatory for male
    // registrants (optional for women); the ID document is mandatory for all.
    public const string VisitorFaceImageMissing = "VISITOR_FACE_IMAGE_MISSING";

    // User profile — nationality. Renamed from VISITOR_NATIONALITY_UNKNOWN so
    // the wire code matches the new domain vocabulary.
    public const string ProfileNationalityUnknown = "PROFILE_NATIONALITY_UNKNOWN";
    /// <summary>A delegate's nationality is not a country invited
    /// to send a delegation (وفد).</summary>
    public const string DelegateCountryNotInvited = "DELEGATE_COUNTRY_NOT_INVITED";

    // Interests (الاهتمامات)
    public const string InterestInvalid = "INTEREST_INVALID";
    public const string InterestNotFound = "INTEREST_NOT_FOUND";
    public const string InterestNameDuplicate = "INTEREST_NAME_DUPLICATE";

    // Roles — admin CRUD over the existing SimfRole + RolePermission +
    // Permission entities; no schema change.
    public const string RoleInvalid = "ROLE_INVALID";
    public const string RoleNotFound = "ROLE_NOT_FOUND";
    public const string RoleNameDuplicate = "ROLE_NAME_DUPLICATE";
    public const string RoleIsBaseline = "ROLE_IS_BASELINE";
    public const string RoleInUse = "ROLE_IN_USE";

    // Themes — the programme themes / pillars.
    public const string ThemeInvalid = "THEME_INVALID";
    public const string ThemeNotFound = "THEME_NOT_FOUND";
    public const string ThemeCodeDuplicate = "THEME_CODE_DUPLICATE";
    public const string ThemeInUse = "THEME_IN_USE";

    // Halls — the venue halls.
    public const string HallInvalid = "HALL_INVALID";
    public const string HallNotFound = "HALL_NOT_FOUND";
    public const string HallCodeDuplicate = "HALL_CODE_DUPLICATE";
    public const string HallInUse = "HALL_IN_USE";
    // A hall Capacity reduction below what the hall
    // already commits (its seat-layout total, or the largest active reservation
    // count on any single session held in the hall).
    public const string HallCapacityBelowUsage = "HALL_CAPACITY_BELOW_USAGE";

    // Countries.
    public const string CountryInvalid = "COUNTRY_INVALID";
    public const string CountryNotFound = "COUNTRY_NOT_FOUND";
    public const string CountryCodeDuplicate = "COUNTRY_CODE_DUPLICATE";
    public const string CountryIdDuplicate = "COUNTRY_ID_DUPLICATE";
    public const string CountryInUse = "COUNTRY_IN_USE";

    // Speakers.
    public const string SpeakerInvalid = "SPEAKER_INVALID";
    public const string SpeakerNotFound = "SPEAKER_NOT_FOUND";
    public const string SpeakerCodeDuplicate = "SPEAKER_CODE_DUPLICATE";
    public const string SpeakerInUse = "SPEAKER_IN_USE";

    // Speaker presentations.
    public const string SpeakerPresentationInvalid = "SPEAKER_PRESENTATION_INVALID";
    public const string SpeakerPresentationNotFound = "SPEAKER_PRESENTATION_NOT_FOUND";

    // System configuration settings.
    public const string SystemSettingInvalid = "SYSTEM_SETTING_INVALID";
    public const string SystemSettingNotFound = "SYSTEM_SETTING_NOT_FOUND";
    public const string SystemSettingKeyDuplicate = "SYSTEM_SETTING_KEY_DUPLICATE";

    // Organization / About profile.
    public const string OrganizationProfileInvalid = "ORGANIZATION_PROFILE_INVALID";

    // Venue map nodes.
    public const string VenueMapNodeInvalid = "VENUE_MAP_NODE_INVALID";
    public const string VenueMapNodeNotFound = "VENUE_MAP_NODE_NOT_FOUND";

    // Sponsors.
    public const string SponsorInvalid = "SPONSOR_INVALID";
    public const string SponsorNotFound = "SPONSOR_NOT_FOUND";
    public const string SponsorDuplicate = "SPONSOR_DUPLICATE";

    // Media partners.
    public const string MediaPartnerNameDuplicate = "MEDIA_PARTNER_NAME_DUPLICATE";

    // Booths — the Exhibition module + the 2D venue map.
    public const string BoothInvalid = "BOOTH_INVALID";
    public const string BoothNotFound = "BOOTH_NOT_FOUND";
    public const string BoothCodeDuplicate = "BOOTH_CODE_DUPLICATE";
    // A booth still marked by an active venue-map node cannot be
    // deactivated (the map node would orphan). Mirrors ContactInUse.
    public const string BoothInUse = "BOOTH_IN_USE";

    // News — PR / marketing news. Promoted from the AdminNewsService
    // module-local consts; string values are the wire contract and must stay
    // verbatim.
    public const string NewsInvalid = "NEWS_INVALID";
    public const string NewsNotFound = "NEWS_NOT_FOUND";
    public const string NewsTitleDuplicate = "NEWS_TITLE_DUPLICATE";

    // FAQ.
    public const string FaqInvalid = "FAQ_INVALID";
    public const string FaqGroupNotFound = "FAQ_GROUP_NOT_FOUND";
    public const string FaqEntryNotFound = "FAQ_ENTRY_NOT_FOUND";

    // Dynamic ratings — config CRUD + attendee submission.
    public const string RatingInvalid = "RATING_INVALID";
    public const string RatingTypeNotFound = "RATING_TYPE_NOT_FOUND";
    public const string RatingTypeIsSystem = "RATING_TYPE_IS_SYSTEM";
    public const string RatingTypeCodeTaken = "RATING_TYPE_CODE_TAKEN";
    public const string RatingGroupNotFound = "RATING_GROUP_NOT_FOUND";
    public const string RatingQuestionNotFound = "RATING_QUESTION_NOT_FOUND";
    public const string RatingTargetRequired = "RATING_TARGET_REQUIRED";
    public const string RatingTargetNotFound = "RATING_TARGET_NOT_FOUND";
    // A rating may only be submitted for something the user
    // attended (in-hall check-in, or a venue-gate check-in for day/overall scopes).
    public const string RatingNotAttended = "RATING_NOT_ATTENDED";

    // Media gallery. Promoted from the module-local MediaErrorCodes; string
    // values are the wire contract.
    public const string MediaNotFound = "media_not_found";
    public const string MediaInvalid = "media_invalid";

    // Archive — past editions. Promoted from the AdminArchiveService
    // module-local consts; string values are the wire contract.
    public const string ArchiveEditionNotFound = "archive_edition_not_found";
    public const string ArchiveEditionInvalid = "archive_edition_invalid";
    public const string ArchiveEditionYearDuplicate = "archive_edition_year_duplicate";

    // Operations toggles — the registration gate + archive visibility
    // singletons.
    public const string RegistrationClosed = "REGISTRATION_CLOSED";

    // Session questions + moderator grants. A per-session moderator grant is
    // distinct from MobileAppRole.Moderator.
    public const string SessionQuestionInvalid = "SESSION_QUESTION_INVALID";
    public const string SessionQuestionNotFound = "SESSION_QUESTION_NOT_FOUND";
    public const string SessionNotLiveForQuestions = "SESSION_NOT_LIVE_FOR_QUESTIONS";
    public const string SessionModeratorNotAssigned = "SESSION_MODERATOR_NOT_ASSIGNED";
    public const string SessionModeratorAlreadyAssigned = "SESSION_MODERATOR_ALREADY_ASSIGNED";
    // The target account is not eligible to moderate (its profile
    // type does not carry MobileAppRole.Moderator).
    public const string SessionModeratorNotEligible = "SESSION_MODERATOR_NOT_ELIGIBLE";

    // Venue self-assert — the self-assert toggle is what decides whether the
    // caller counts as being at the venue.
    public const string NotAtVenue = "NOT_AT_VENUE";

    // Speaker meeting requests.
    public const string SpeakerMeetingRequestInvalid = "SPEAKER_MEETING_REQUEST_INVALID";
    public const string SpeakerMeetingRequestNotFound = "SPEAKER_MEETING_REQUEST_NOT_FOUND";
    /// <summary>The speaker availability window was not found.</summary>
    public const string SpeakerAvailabilityWindowNotFound = "SPEAKER_AVAILABILITY_WINDOW_NOT_FOUND";
    /// <summary>The hall availability window was not found.</summary>
    public const string HallAvailabilityWindowNotFound = "HALL_AVAILABILITY_WINDOW_NOT_FOUND";
    /// <summary>The delegation availability window was not found.</summary>
    public const string DelegationAvailabilityWindowNotFound = "DELEGATION_AVAILABILITY_WINDOW_NOT_FOUND";
    /// <summary>Invalid delegation meeting request (subject/count/self).</summary>
    public const string DelegationMeetingRequestInvalid = "DELEGATION_MEETING_REQUEST_INVALID";
    /// <summary>The delegation meeting request was not found.</summary>
    public const string DelegationMeetingRequestNotFound = "DELEGATION_MEETING_REQUEST_NOT_FOUND";
    public const string SpeakerMeetingRequestsNotAllowed = "SPEAKER_MEETING_REQUESTS_NOT_ALLOWED";
    public const string SpeakerMeetingRequestStatusInvalid = "SPEAKER_MEETING_REQUEST_STATUS_INVALID";
    /// <summary>The speaker has no free meeting slot left, so the request cannot be
    /// sent. Covers BOTH reasons at once: the speaker has no active future
    /// availability window at all, and every slot the windows offer is already past
    /// or taken.</summary>
    public const string SpeakerMeetingNoAvailability = "SPEAKER_MEETING_NO_AVAILABILITY";
    /// <summary>The target delegation has no free meeting slot left (no active
    /// future window, or every slot is past or taken), so the request cannot be
    /// sent.</summary>
    public const string DelegationMeetingNoAvailability = "DELEGATION_MEETING_NO_AVAILABILITY";
    /// <summary>A speaker action-link token is unusable: not found, expired,
    /// already used, or its request is no longer awaiting the speaker.
    /// Deliberately NEUTRAL — the same code for every reason so the response
    /// never leaks which one it was.</summary>
    public const string MeetingActionTokenInvalid = "MEETING_ACTION_TOKEN_INVALID";
    /// <summary>The speaker has no contact email on file, so the
    /// double-opt-in Approve/Reject links could never be delivered. The approve /
    /// resend path fails LOUDLY with this code instead of silently stranding the
    /// request in <c>AwaitingSpeaker</c> with tokens nobody will ever receive.</summary>
    public const string SpeakerMeetingContactMissing = "SPEAKER_MEETING_CONTACT_MISSING";
    /// <summary><c>MeetingLinks:PublicWebBaseUrl</c> is unconfigured, so the
    /// speaker confirmation link cannot be built. Missing link configuration is a hard
    /// failure on the approve / resend path, never a silent skip.</summary>
    public const string MeetingLinksNotConfigured = "MEETING_LINKS_NOT_CONFIGURED";

    // Unified requests (الطلبات).
    public const string ParticipationDocumentRequestInvalid = "PARTICIPATION_DOCUMENT_REQUEST_INVALID";
    public const string ParticipationDocumentRequestNotFound = "PARTICIPATION_DOCUMENT_REQUEST_NOT_FOUND";
    public const string ParticipationDocumentRequestStatusInvalid = "PARTICIPATION_DOCUMENT_REQUEST_STATUS_INVALID";
    public const string BadgeUpdateRequestInvalid = "BADGE_UPDATE_REQUEST_INVALID";
    public const string BadgeUpdateRequestNotFound = "BADGE_UPDATE_REQUEST_NOT_FOUND";
    public const string BadgeUpdateRequestStatusInvalid = "BADGE_UPDATE_REQUEST_STATUS_INVALID";
    /// <summary>Self-cancel target not found / not owned by the caller.</summary>
    public const string AppRequestNotFound = "APP_REQUEST_NOT_FOUND";
    /// <summary>The request kind is not self-cancellable, or it is no
    /// longer Pending.</summary>
    public const string AppRequestNotCancellable = "APP_REQUEST_NOT_CANCELLABLE";
    /// <summary>An admin tried to respond to a request that is no longer
    /// Pending (already Accepted / Rejected / Cancelled). Guards double-response and
    /// the side effects a re-decision would replay.</summary>
    public const string AppRequestAlreadyResponded = "APP_REQUEST_ALREADY_RESPONDED";
    /// <summary>The requester already has an open Pending request for the
    /// same target (speaker / delegation), so a duplicate submission is rejected.</summary>
    public const string AppRequestDuplicatePending = "APP_REQUEST_DUPLICATE_PENDING";

    // Seat reservations.
    public const string SeatLayoutInvalid = "SEAT_LAYOUT_INVALID";
    public const string SeatLayoutMissing = "SEAT_LAYOUT_MISSING";
    // A layout change would strand active reservations that reference a row/
    // seat the new layout no longer contains.
    public const string SeatLayoutHasReservations = "SEAT_LAYOUT_HAS_RESERVATIONS";
    public const string SeatOutOfBounds = "SEAT_OUT_OF_BOUNDS";
    public const string SeatAlreadyReserved = "SEAT_ALREADY_RESERVED";
    public const string SeatAlreadyOwnedBySession = "SEAT_ALREADY_OWNED_BY_SESSION";
    public const string SeatReservationNotFound = "SEAT_RESERVATION_NOT_FOUND";
    public const string SeatCapacityExceeded = "SEAT_CAPACITY_EXCEEDED";
    public const string SeatSessionFull = "SEAT_SESSION_FULL";
    // Seat-selection-mode mismatch between the request and the session's
    // effective mode (Session.SeatSelectionModeOverride ?? Hall.SeatSelectionMode).
    public const string SeatSelectionRequired = "SEAT_SELECTION_REQUIRED";
    public const string OpenSeatingOnly = "OPEN_SEATING_ONLY";
    // Seat TIER eligibility. NOT_ELIGIBLE: the visitor's profile tier does
    // not reach the seat's tier (a non-VIP visitor picked a VIP seat). RESERVED: a
    // VVIP protocol seat, which nobody may self-reserve — an administrator assigns
    // it manually with a guest hint.
    public const string SeatTierNotEligible = "SEAT_TIER_NOT_ELIGIBLE";
    public const string SeatTierReserved = "SEAT_TIER_RESERVED";
    // A CHANGE-SEAT request whose destination is the seat the caller already
    // holds. Distinct from SEAT_ALREADY_RESERVED (someone else has it) so the app
    // can say "you are already sitting there" instead of "that seat is taken".
    public const string SeatMoveSameSeat = "SEAT_MOVE_SAME_SEAT";

    // Booking approval workflow.
    // The approval queue was removed on 2026-07-18 (bookings
    // auto-confirm), so BookingNotFound, BookingNotPending and
    // BookingRejectionReasonRequired are VESTIGIAL: nothing raises them any more,
    // because there is no approve/reject action left (a missing reservation throws
    // SEAT_RESERVATION_NOT_FOUND). Kept as reserved codes rather than deleted — they
    // are published in the API spec and are the landing spot if an approval step
    // returns — but do not wire new behaviour to them. BookingOverlap /
    // BookingSessionStarted / BookingSessionEnded are live.
    public const string BookingOverlap = "BOOKING_OVERLAP";
    public const string BookingNotFound = "BOOKING_NOT_FOUND";
    public const string BookingNotPending = "BOOKING_NOT_PENDING";
    public const string BookingRejectionReasonRequired = "BOOKING_REJECTION_REASON_REQUIRED";
    public const string BookingSessionStarted = "BOOKING_SESSION_STARTED";
    // A create-booking attempt on a session that has
    // already ENDED (now >= Session.End). A started-but-live session stays bookable.
    public const string BookingSessionEnded = "BOOKING_SESSION_ENDED";

    // Flexible hall config + B2B/B2C business meetings.
    public const string HallPurposeInvalid = "HALL_PURPOSE_INVALID";
    public const string HallNotMeetingPurpose = "HALL_NOT_MEETING_PURPOSE";
    public const string MeetingTableInvalid = "MEETING_TABLE_INVALID";
    public const string MeetingTableNotFound = "MEETING_TABLE_NOT_FOUND";
    public const string MeetingTableCodeDuplicate = "MEETING_TABLE_CODE_DUPLICATE";
    public const string HallAllocationInvalid = "HALL_ALLOCATION_INVALID";
    public const string HallAllocationOverlap = "HALL_ALLOCATION_OVERLAP";
    public const string HallAllocationNotFound = "HALL_ALLOCATION_NOT_FOUND";
    public const string BusinessMeetingInvalid = "BUSINESS_MEETING_INVALID";
    public const string BusinessMeetingNotFound = "BUSINESS_MEETING_NOT_FOUND";
    public const string BusinessMeetingNotConfirmed = "BUSINESS_MEETING_NOT_CONFIRMED";
    public const string BusinessMeetingTableConflict = "BUSINESS_MEETING_TABLE_CONFLICT";
    public const string BusinessMeetingParticipantConflict = "BUSINESS_MEETING_PARTICIPANT_CONFLICT";
    public const string MeetingParticipantInvalid = "MEETING_PARTICIPANT_INVALID";
    public const string MeetingCapacityExceeded = "MEETING_CAPACITY_EXCEEDED";

    // Shared contact directory.
    public const string ContactInvalid = "CONTACT_INVALID";
    public const string ContactInUse = "CONTACT_IN_USE";

    // Centralised AI module.
    public const string AiPromptInvalid = "AI_PROMPT_INVALID";
    public const string AiPromptNotFound = "AI_PROMPT_NOT_FOUND";
    public const string AiPromptKeyDuplicate = "AI_PROMPT_KEY_DUPLICATE";
    // Transactional email templates.
    public const string EmailTemplateNotFound = "EMAIL_TEMPLATE_NOT_FOUND";
    public const string EmailTemplateInvalid = "EMAIL_TEMPLATE_INVALID";
    public const string AiProviderNotConfigured = "AI_PROVIDER_NOT_CONFIGURED";
    public const string AiProviderFailed = "AI_PROVIDER_FAILED";
    public const string AiInputInvalid = "AI_INPUT_INVALID";
    public const string AiFeatureDisabled = "AI_FEATURE_DISABLED";
    // Server-side subtitle fetch from a video (YouTube) failed or the
    // server has no egress to reach it (paste / upload the subtitle instead).
    public const string SubtitleFetchFailed = "SUBTITLE_FETCH_FAILED";

    // CMS: ContentBlock + Banner.
    public const string ContentBlockInvalid = "CONTENT_BLOCK_INVALID";
    public const string ContentBlockNotFound = "CONTENT_BLOCK_NOT_FOUND";
    public const string ContentBlockKeyDuplicate = "CONTENT_BLOCK_KEY_DUPLICATE";
    /// <summary>A content block's markdown could not be rendered safely:
    /// the sanitizing pipeline stripped or rejected the submitted markup. Raised on
    /// the admin write path so an admin-editable field can never reach the public
    /// surface as unsanitised HTML.</summary>
    public const string ContentMarkdownUnsafe = "CONTENT_MARKDOWN_UNSAFE";
    public const string BannerInvalid = "BANNER_INVALID";
    public const string BannerNotFound = "BANNER_NOT_FOUND";
    public const string BannerInvalidTimeWindow = "BANNER_INVALID_TIME_WINDOW";

    // Device keys / biometric sign-in.
    public const string DeviceKeyInvalid = "DEVICE_KEY_INVALID";
    public const string DeviceKeyNotFound = "DEVICE_KEY_NOT_FOUND";
    public const string DeviceKeyAlgorithmUnsupported = "DEVICE_KEY_ALGORITHM_UNSUPPORTED";
    public const string DeviceKeyRevoked = "DEVICE_KEY_REVOKED";
    public const string DeviceKeyChallengeInvalid = "DEVICE_KEY_CHALLENGE_INVALID";
    public const string DeviceKeySignatureInvalid = "DEVICE_KEY_SIGNATURE_INVALID";
    public const string DeviceKeyOwnerUnavailable = "DEVICE_KEY_OWNER_UNAVAILABLE";
    // Emailed-OTP step-up before enrolling a biometric device key.
    public const string BiometricStepUpRequired = "BIOMETRIC_STEP_UP_REQUIRED";
    public const string BiometricStepUpInvalid = "BIOMETRIC_STEP_UP_INVALID";

    // Invitations + VIP notify — the public-relations module.
    public const string InvitationInvalid = "INVITATION_INVALID";
    public const string InvitationNotFound = "INVITATION_NOT_FOUND";
    public const string InvitationTargetNotFound = "INVITATION_TARGET_NOT_FOUND";
    public const string InvitationStateInvalid = "INVITATION_STATE_INVALID";
    public const string VipNotifyEmpty = "VIP_NOTIFY_EMPTY";
    public const string VipNotifyTooLarge = "VIP_NOTIFY_TOO_LARGE";

    // Notification broadcasts (Control Panel "Announcements" desk).
    public const string BroadcastInvalid = "BROADCAST_INVALID";
    public const string BroadcastNotFound = "BROADCAST_NOT_FOUND";

    // Sessions — programme sessions tied to a Hall + M-to-M Speakers +
    // M-to-M Themes.
    public const string SessionInvalid = "SESSION_INVALID";
    public const string SessionNotFound = "SESSION_NOT_FOUND";
    public const string SessionCodeDuplicate = "SESSION_CODE_DUPLICATE";
    public const string SessionInvalidTimeWindow = "SESSION_INVALID_TIME_WINDOW";
    public const string SessionHallNotFound = "SESSION_HALL_NOT_FOUND";
    // Hall geofence invalid (partial set, or out-of-range lat/lon/radius).
    public const string HallGeofenceInvalid = "HALL_GEOFENCE_INVALID";
    // GPS arrival attempted at a hall with no geofence configured.
    public const string HallGeofenceNotConfigured = "HALL_GEOFENCE_NOT_CONFIGURED";
    // Operator QR-door-scan — unknown badge / non-approved attendee.
    public const string AttendeeQrUnknown = "ATTENDEE_QR_UNKNOWN";
    public const string AttendeeNotApproved = "ATTENDEE_NOT_APPROVED";
    // The signed-in account carries no attendee profile. Attendance, seating and
    // lead capture are keyed by the profile, which is the row every ATTENDEE has;
    // an admin-typed account has none and is therefore not an attendee at all.
    // Additive code; the app/CP render the server message.
    public const string AttendeeProfileMissing = "ATTENDEE_PROFILE_MISSING";
    // SESSION_NOT_LIVE: hall arrival attempted outside the session's live time
    // window (± grace). HALL_AT_CAPACITY: the hall is at its physical capacity.
    // Additive codes; the app/CP render the server message and fall back on an
    // unknown code.
    public const string SessionNotLive = "SESSION_NOT_LIVE";
    public const string HallAtCapacity = "HALL_AT_CAPACITY";
    public const string SessionSpeakerNotFound = "SESSION_SPEAKER_NOT_FOUND";
    public const string SessionThemeNotFound = "SESSION_THEME_NOT_FOUND";
    // A session must declare a Type (Workshop/Session/Event) and a
    // non-Event session needs at least one speaker (both grandfathered on edit).
    public const string SessionTypeRequired = "SESSION_TYPE_REQUIRED";
    public const string SessionSpeakerRequired = "SESSION_SPEAKER_REQUIRED";
    // Illegal broadcast-lifecycle transition (e.g. skipping a step).
    public const string SessionStatusTransitionInvalid = "SESSION_STATUS_TRANSITION_INVALID";
    // Recording upload rejected (empty / too large) / not found.
    public const string SessionRecordingInvalid = "SESSION_RECORDING_INVALID";
    public const string SessionRecordingNotFound = "SESSION_RECORDING_NOT_FOUND";
    // AI session-summary / محضر validation + lookup.
    public const string SessionSummaryInvalid = "SESSION_SUMMARY_INVALID";
    public const string SessionSummaryNotFound = "SESSION_SUMMARY_NOT_FOUND";
    // Session admin guards.
    public const string SessionHasActiveBookings = "SESSION_HAS_ACTIVE_BOOKINGS";
    public const string SessionCapacityBelowBookings = "SESSION_CAPACITY_BELOW_BOOKINGS";
    public const string SessionHallTimeOverlap = "SESSION_HALL_TIME_OVERLAP";
    public const string SessionStatusGuardFailed = "SESSION_STATUS_GUARD_FAILED";

    // Gates — the Gate Module.
    public const string GateInvalid = "GATE_INVALID";
    public const string GateNotFound = "GATE_NOT_FOUND";
    public const string GateCodeDuplicate = "GATE_CODE_DUPLICATE";
    // GATE_INACTIVE (503) is retired: a scan at an inactive gate
    // is a RECORDED denial at HTTP 200 (DenialReasonCode.GateInactiveAtScan),
    // never an envelope failure, so no endpoint ever emitted this code. Kept in
    // the published vocabulary so an older client that still branches on it
    // keeps compiling / decoding; nothing produces it.
    [Obsolete("Never emitted — an inactive gate is denied at HTTP 200 with " +
              "GATE_INACTIVE_AT_SCAN (DEF-STF-008).")]
    public const string GateInactive = "GATE_INACTIVE";
    public const string GateOperatorNotAssigned = "GATE_OPERATOR_NOT_ASSIGNED";
    public const string GateAssignmentInvalid = "GATE_ASSIGNMENT_INVALID";
    public const string GateProfileTypeInvalid = "GATE_PROFILE_TYPE_INVALID";
    // The hall bound to a hall-door gate was not found or is
    // inactive (validated on gate create/update).
    public const string GateHallInvalid = "GATE_HALL_INVALID";
    public const string IdempotencyKeyConflict = "IDEMPOTENCY_KEY_CONFLICT";
    public const string GateFailureCircuitOpen = "GATE_FAILURE_CIRCUIT_OPEN";

    // Offline badge desk.
    /// <summary>The offline upload is not armed. Per-request, not a permission
    /// failure: the caller holds the desk permission but the capability is off.</summary>
    public const string OfflineUploadDisabled = "OFFLINE_UPLOAD_DISABLED";

    /// <summary>A batch item's sequence is already on an account. Reported per
    /// item as AlreadyUploaded, so a retried upload reconciles instead of
    /// creating a second account for the same printed badge.</summary>
    public const string OfflineBadgeSequenceTaken = "OFFLINE_BADGE_SEQUENCE_TAKEN";

    /// <summary>The sequence is outside the range a badge id can express, or the
    /// profile-type code on the badge is not a live profile type.</summary>
    public const string OfflineBadgeInvalid = "OFFLINE_BADGE_INVALID";

    // Exhibitors + account provisioning.
    public const string ExhibitorInvalid = "EXHIBITOR_INVALID";
    public const string ExhibitorNotFound = "EXHIBITOR_NOT_FOUND";
    public const string ExhibitorInactive = "EXHIBITOR_INACTIVE";
    public const string ExhibitorAccountInvalid = "EXHIBITOR_ACCOUNT_INVALID";

    // Attaching an EXISTING account to an exhibitor
    // (POST /admin/exhibitors/{id}/accounts/link). No account is registered under
    // the supplied email (404).
    public const string ExhibitorAccountNotFound = "EXHIBITOR_ACCOUNT_NOT_FOUND";

    // The account exists but does not carry an active exhibitor-mapped
    // profile type, so linking it would hand it booth tools it cannot use (409).
    public const string ExhibitorAccountNotEligible = "EXHIBITOR_ACCOUNT_NOT_ELIGIBLE";

    // The account already holds an active ExhibitorMembership; an account
    // belongs to at most one booth at a time (409, mirrors the filtered unique
    // index on ExhibitorMembership.UserId).
    public const string ExhibitorAccountAlreadyLinked = "EXHIBITOR_ACCOUNT_ALREADY_LINKED";

    // Organisations — the Saudi-companies lookup with government Excel
    // bulk-import; the visitor الجهة picker reads from this table.
    public const string OrganisationInvalid = "ORGANISATION_INVALID";
    public const string OrganisationNotFound = "ORGANISATION_NOT_FOUND";
    public const string OrganisationImportFailed = "ORGANISATION_IMPORT_FAILED";

    // Regions (administrative-regions lookup; the app region picker reads this
    // table). Code is the stable unique lookup key.
    public const string RegionInvalid = "REGION_INVALID";
    public const string RegionNotFound = "REGION_NOT_FOUND";

    // Networking connections.
    public const string ConnectionInvalid = "CONNECTION_INVALID";
    public const string ConnectionNotFound = "CONNECTION_NOT_FOUND";
    public const string ConnectionAlreadyExists = "CONNECTION_ALREADY_EXISTS";
    public const string ConnectionSelf = "CONNECTION_SELF";
    public const string ConnectionTargetNotFound = "CONNECTION_TARGET_NOT_FOUND";

    // Session categories.
    public const string SessionCategoryInvalid = "SESSION_CATEGORY_INVALID";
    public const string SessionCategoryNotFound = "SESSION_CATEGORY_NOT_FOUND";
    // Programme days (date + title + logo) admin CRUD.
    public const string ProgrammeDayInvalid = "PROGRAMME_DAY_INVALID";
    public const string ProgrammeDayNotFound = "PROGRAMME_DAY_NOT_FOUND";
}
