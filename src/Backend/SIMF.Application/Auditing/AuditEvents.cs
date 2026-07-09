namespace SIMF.Application.Auditing;

/// <summary>
/// The stable audit event-type names (SIMF-FDS-001 section 9). Names are
/// defined here once and never written as literals elsewhere.
/// </summary>
public static class AuditEvents
{
    public const string SignUpSucceeded = "SignUp.Succeeded";
    public const string SignUpDuplicateEmail = "SignUp.DuplicateEmail";
    // D-198 — re-sign-up of an unverified account: password replaced + a
    // fresh verification code issued (enumeration-resistant sign-up).
    public const string SignUpRestartedUnverified = "SignUp.RestartedUnverified";
    // D-198 — sign-up attempt against an existing *verified* account: the
    // owner is emailed a heads-up; the account itself is never touched and
    // the API returns the same generic response a new sign-up would.
    public const string SignUpExistingAccountDeflected = "SignUp.ExistingAccountDeflected";
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
    public const string SignInPasswordChangeRequired = "SignIn.PasswordChangeRequired";
    // A7-13 (NCA) — sign-in found the password older than the configured max age
    // and forced a change. Distinct from a seeded/admin-rotated forced change so
    // SOC can see expiry-driven rotations.
    public const string SignInPasswordExpired = "SignIn.PasswordExpired";
    // D-206: a Control Panel sign-in with a forced-change credential was handed a
    // single-use password-change ticket (in place of the 403). The completion is
    // audited as PasswordChanged, like any other password change.
    public const string SignInPasswordChangeTicketIssued = "SignIn.PasswordChangeTicketIssued";
    public const string SignInWrongSurface = "SignIn.WrongSurface";
    public const string SignInSecondFactorIssued = "SignIn.SecondFactorIssued";
    public const string SignInSecondFactorFailed = "SignIn.SecondFactorFailed";
    public const string SignInSecondFactorRejected = "SignIn.SecondFactorRejected";
    public const string SignInSucceeded = "SignIn.Succeeded";
    // Part B — badge-QR activation (passwordless account sets its first password).
    public const string BadgeActivationStarted = "BadgeActivation.Started";
    public const string BadgeActivationCompleted = "BadgeActivation.Completed";
    public const string BadgeActivationFailed = "BadgeActivation.Failed";
    // P10 — D-051 (extended D-198): a non-approved user signed in
    // (EmailVerified, PendingApproval or Rejected). They got tokens +
    // AccountStateInfo; routed to the profile-form / state-banner page by
    // the front-end. SOC needs to spot these.
    public const string SignInAsGuest = "SignIn.AsGuest";
    public const string RefreshTokenIssued = "RefreshToken.Issued";
    public const string RefreshTokenRotated = "RefreshToken.Rotated";
    public const string RefreshTokenReused = "RefreshToken.Reused";
    public const string RefreshTokenRejected = "RefreshToken.Rejected";
    public const string AccessTokenRejected = "AccessToken.Rejected";
    // A1-12 (NCA Secure Application-Development Standard) — an authenticated
    // request failed authorization (a 403: missing permission, or a
    // non-Approved account state). NCA requires every failed access-control
    // decision to be logged; without this the 401 path was audited but a
    // denied permission left no trail.
    public const string AuthorizationDenied = "Authorization.Denied";
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
    // A1-19 (NCA) — the daily sweep disabled an account for inactivity beyond the
    // configured threshold. A system action (no actor).
    public const string AccountDormantDisabled = "Account.DormantDisabled";

    // H10 — D-065: an email-enqueue failure that lands AFTER the matching
    // code row is already persisted to the DB. The success audit was
    // already written; this row tells SOC the user never actually got
    // the message even though everything else looked clean.
    public const string EmailEnqueueFailed = "Email.EnqueueFailed";

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

    // Admin-driven user management (myComment #33 — D-041, D-042)
    public const string AdminTwoFactorReset = "Admin.TwoFactorReset";
    public const string AdminTwoFactorResetFailed = "Admin.TwoFactorResetFailed";
    public const string AdminUserCreated = "Admin.UserCreated";
    public const string AdminUserCreateFailed = "Admin.UserCreateFailed";
    // P1.3 (D-214) — admin edit of a Visitor / Other account.
    public const string AdminUserUpdated = "Admin.UserUpdated";
    public const string AdminUserUpdateFailed = "Admin.UserUpdateFailed";

    // Admin-driven bulk actions (D-044 b)
    public const string AdminUserDeleted = "Admin.UserDeleted";
    public const string AdminUserDeleteFailed = "Admin.UserDeleteFailed";
    public const string AdminUserDuplicated = "Admin.UserDuplicated";
    public const string AdminUsersExported = "Admin.UsersExported";
    public const string AdminUsersImported = "Admin.UsersImported";

    // P1.6 — XLSX export of the read-only admin grids (filtered result set).
    public const string AdminOperationLogExported = "Admin.OperationLogExported";
    public const string AdminAttendeesExported = "Admin.AttendeesExported";

    // User profile (myComment #18 — D-046; P8 renamed from VisitorProfile.*)
    public const string UserProfileSaved = "UserProfile.Saved";
    public const string UserProfileIdImageUploaded = "UserProfile.IdImageUploaded";
    public const string UserProfileIdImageRejected = "UserProfile.IdImageRejected";
    // A9 (PII) — every admin READ/disclosure of a subject's ID-document image.
    public const string UserProfileIdImageViewed = "UserProfile.IdImageViewed";
    // V-1 (D-429) — VVIP/VIP welcome photo (موج) upload.
    public const string UserProfileVipPhotoUploaded = "UserProfile.VipPhotoUploaded";
    // D-568 (Wave C S4, PII) — every admin READ/disclosure of a subject's VIP photo.
    public const string UserProfileVipPhotoViewed = "UserProfile.VipPhotoViewed";

    // Interests (P9 — D-050; الاهتمامات)
    public const string InterestCreated = "Interest.Created";
    public const string InterestUpdated = "Interest.Updated";
    public const string InterestDeactivated = "Interest.Deactivated";

    // ProfileTypes admin CRUD (D-115)
    public const string ProfileTypeCreated = "ProfileType.Created";
    public const string ProfileTypeUpdated = "ProfileType.Updated";
    public const string ProfileTypeDeactivated = "ProfileType.Deactivated";

    // D-127 — on-site walk-in registration desk
    public const string AdminWalkInRegistered = "Admin.WalkInRegistered";
    public const string AdminWalkInRegisterFailed = "Admin.WalkInRegisterFailed";
    // D-473 (#10): bulk-generate placeholder badges (by profile type + count).
    public const string AdminBulkBadgesGenerated = "Admin.BulkBadgesGenerated";

    // Logs (P6 — per-project log files + CP viewer)
    public const string AdminLogViewed = "Admin.LogViewed";
    public const string AdminLogDownloaded = "Admin.LogDownloaded";

    // Roles (D-134 Sprint A — admin CRUD over existing SimfRole +
    // RolePermission + Permission entities; pure P2 — no schema change)
    public const string RoleCreated = "Role.Created";
    public const string RoleUpdated = "Role.Updated";
    public const string RoleDeleted = "Role.Deleted";
    // Issue-1 — an admin changed a custom role's permission grants.
    public const string RolePermissionsUpdated = "Role.PermissionsUpdated";
    // Issue-1 — an admin changed which roles a user holds.
    public const string UserRolesUpdated = "User.RolesUpdated";

    // Themes (D-134 Sprint B — programme themes, SIMF-FDS-004 §5.1)
    public const string ThemeCreated = "Theme.Created";
    public const string ThemeUpdated = "Theme.Updated";
    public const string ThemeDeactivated = "Theme.Deactivated";

    // Halls (D-134 Sprint B — venue halls, SIMF-FDS-004 §5.2)
    public const string HallCreated = "Hall.Created";
    public const string HallUpdated = "Hall.Updated";
    public const string HallDeactivated = "Hall.Deactivated";

    // Countries (D-151 — admin-managed country lookup)
    public const string CountryCreated = "Country.Created";
    public const string CountryUpdated = "Country.Updated";
    public const string CountryDeactivated = "Country.Deactivated";

    // Speakers (D-151 — programme speakers, SIMF-DAT-001 §5.4)
    public const string SpeakerCreated = "Speaker.Created";
    public const string SpeakerUpdated = "Speaker.Updated";
    public const string SpeakerDeactivated = "Speaker.Deactivated";

    // Speaker presentations (P2.3 / D-228 — FR-407)
    public const string SpeakerPresentationUploaded = "SpeakerPresentation.Uploaded";
    public const string SpeakerPresentationDeleted = "SpeakerPresentation.Deleted";

    // Unified media assets (D-357 — the one upload/download pipeline for speaker
    // photos, company / sponsor / media-partner logos, archive covers, news images)
    public const string AssetUploaded = "Asset.Uploaded";
    public const string AssetLinked = "Asset.Linked";
    public const string AssetRemoved = "Asset.Removed";
    public const string AssetRestored = "Asset.Restored";

    // D-568 — the centralized file store. Every file action is audited, including
    // a denied private download (SAMA E-16/17 / NCA ECC 2-12). Public-file reads
    // are not audited per-row (they would flood the log).
    public const string FileUploaded = "File.Uploaded";
    public const string FileLinked = "File.Linked";
    public const string FileDownloaded = "File.Downloaded";
    public const string FileAccessDenied = "File.AccessDenied";
    public const string FileIntegrityFailed = "File.IntegrityFailed";
    public const string FileDeleted = "File.Deleted";
    public const string FileSecurelyDestroyed = "File.SecurelyDestroyed";

    // System configuration settings (P2.4 / D-229 — FDS-012 §5.5)
    public const string SystemSettingCreated = "SystemSetting.Created";
    public const string SystemSettingUpdated = "SystemSetting.Updated";
    public const string SystemSettingDeactivated = "SystemSetting.Deactivated";

    // Organization / About profile (D-495)
    public const string OrganizationProfileUpdated = "OrganizationProfile.Updated";

    // Venue map nodes (P2.5 / D-230 — FR-605)
    public const string VenueMapNodeCreated = "VenueMapNode.Created";
    public const string VenueMapNodeUpdated = "VenueMapNode.Updated";
    public const string VenueMapNodeDeactivated = "VenueMapNode.Deactivated";

    // Booths (D-199 — Exhibition module, Mockup page 22 + 2D venue map).
    public const string BoothCreated = "Booth.Created";
    public const string BoothUpdated = "Booth.Updated";
    public const string BoothDeactivated = "Booth.Deactivated";

    // Sponsors (D-199 — event sponsors, Mockup page 23)
    public const string SponsorCreated = "Sponsor.Created";
    public const string SponsorUpdated = "Sponsor.Updated";
    public const string SponsorDeactivated = "Sponsor.Deactivated";

    // Sessions (D-165, gap doc G3 — programme sessions)
    public const string SessionCreated = "Session.Created";
    public const string SessionUpdated = "Session.Updated";
    public const string SessionDeactivated = "Session.Deactivated";
    // P3.2 — D-231: broadcast-lifecycle transitions.
    public const string SessionStatusChanged = "Session.StatusChanged";
    public const string SessionPublished = "Session.Published";
    public const string SessionUnpublished = "Session.Unpublished";
    // P3.2b — D-232: session-recording attach / delete.
    public const string SessionRecordingUploaded = "Session.RecordingUploaded";
    public const string SessionRecordingDeleted = "Session.RecordingDeleted";
    // P5.1 — D-241 (FDS-003 §5.4): hall arrival / departure (geofence or QR door scan).
    public const string HallArrivalRecorded = "HallAttendance.ArrivalRecorded";
    public const string HallDepartureRecorded = "HallAttendance.DepartureRecorded";
    // P4.1 — D-238: AI session-summary / محضر committee actions.
    public const string SessionSummaryGenerated = "SessionSummary.Generated";
    public const string SessionSummarySaved = "SessionSummary.Saved";
    public const string SessionSummaryPublished = "SessionSummary.Published";
    public const string SessionSummaryUnpublished = "SessionSummary.Unpublished";
    // D-472 (#9): the team review/approval workflow on the محضر.
    public const string SessionSummarySubmittedForReview = "SessionSummary.SubmittedForReview";
    public const string SessionSummaryApproved = "SessionSummary.Approved";
    public const string SessionSummaryReturnedToDraft = "SessionSummary.ReturnedToDraft";

    // Session questions + moderator grants (D-169, gap doc G6 — PDF §2.7.2)
    public const string SessionQuestionSubmitted = "SessionQuestion.Submitted";
    public const string SessionQuestionHidden = "SessionQuestion.Hidden";
    public const string SessionQuestionUnhidden = "SessionQuestion.Unhidden";
    public const string SessionQuestionPushed = "SessionQuestion.Pushed";
    public const string SessionQuestionReordered = "SessionQuestion.Reordered";
    public const string SessionModeratorAssigned = "SessionModerator.Assigned";
    public const string SessionModeratorRevoked = "SessionModerator.Revoked";

    // Venue self-assert (D-171, gap doc G7 — PDF §2.10)
    public const string SessionQuestionRejectedNotAtVenue = "SessionQuestion.RejectedNotAtVenue";
    // P3.3 — D-212: Scientific-Committee pipeline actions.
    public const string SessionQuestionApproved = "SessionQuestion.Approved";
    public const string SessionQuestionEscalated = "SessionQuestion.Escalated";

    // Networking connections (B6 — D-224: visitor-to-visitor request/accept).
    public const string ConnectionRequested = "Connection.Requested";
    public const string ConnectionAccepted = "Connection.Accepted";
    public const string ConnectionRemoved = "Connection.Removed";

    // Session categories (B9b — D-226: dynamic lookup, FDS-004 §5.4).
    public const string SessionCategoryCreated = "SessionCategory.Created";
    public const string SessionCategoryUpdated = "SessionCategory.Updated";
    public const string SessionCategoryDeactivated = "SessionCategory.Deactivated";

    // D-452 — programme days (date + title + logo).
    public const string ProgrammeDayCreated = "ProgrammeDay.Created";
    public const string ProgrammeDayUpdated = "ProgrammeDay.Updated";
    public const string ProgrammeDayDeactivated = "ProgrammeDay.Deactivated";

    // Device keys / biometric sign-in (D-172, gap doc G10 — PDF §2.5)
    public const string DeviceKeyRegistered = "DeviceKey.Registered";
    public const string DeviceKeyRevoked = "DeviceKey.Revoked";
    public const string SignInWithDeviceKey = "SignIn.WithDeviceKey";
    public const string SignInWithDeviceKeyFailed = "SignIn.WithDeviceKeyFailed";
    // #7a — emailed-OTP step-up guarding biometric device-key enrolment.
    public const string DeviceKeyStepUpIssued = "DeviceKey.StepUpIssued";
    public const string DeviceKeyStepUpRejected = "DeviceKey.StepUpRejected";

    // CMS: ContentBlock + Banner (D-173, gap doc G8 — PDF §1, §2.1)
    public const string ContentBlockUpserted = "ContentBlock.Upserted";
    public const string ContentBlockDeactivated = "ContentBlock.Deactivated";
    public const string BannerCreated = "Banner.Created";
    public const string BannerUpdated = "Banner.Updated";
    public const string BannerDeactivated = "Banner.Deactivated";

    // Speaker meeting requests (D-269 — Mockup page 20). Same SOC rationale as
    // the session-scoped events above: the list carries requester names and the
    // per-record detail/respond reveals the requester email.
    public const string SpeakerMeetingRequestSubmitted = "SpeakerMeetingRequest.Submitted";
    public const string SpeakerMeetingRequestResponded = "SpeakerMeetingRequest.Responded";
    // D-474 (#11, Group G) — speaker availability windows for the VIP-meeting slots.
    public const string SpeakerAvailabilityWindowCreated = "SpeakerAvailabilityWindow.Created";
    public const string SpeakerAvailabilityWindowDeleted = "SpeakerAvailabilityWindow.Deleted";
    // D-715 (item 7, FDS-013 §15 GAP-1) — hall availability windows.
    public const string HallAvailabilityWindowCreated = "HallAvailabilityWindow.Created";
    public const string HallAvailabilityWindowDeleted = "HallAvailabilityWindow.Deleted";
    // D-478 (#11, Group G phase 2) — delegation↔delegation (G2G) meeting requests.
    public const string DelegationMeetingRequestSubmitted = "DelegationMeetingRequest.Submitted";
    public const string DelegationMeetingRequestResponded = "DelegationMeetingRequest.Responded";
    public const string AdminDelegationMeetingRequestsListed = "Admin.DelegationMeetingRequestsListed";
    public const string AdminDelegationMeetingRequestViewed = "Admin.DelegationMeetingRequestViewed";
    public const string AdminSpeakerMeetingRequestsListed = "Admin.SpeakerMeetingRequestsListed";
    public const string AdminSpeakerMeetingRequestViewed = "Admin.SpeakerMeetingRequestViewed";

    // D-500 (Wave 5, الطلبات) — participation-document + badge-update requests
    // (the admin detail/respond reveals the requester email — same SOC rationale).
    public const string ParticipationDocumentRequestSubmitted = "ParticipationDocumentRequest.Submitted";
    public const string ParticipationDocumentRequestResponded = "ParticipationDocumentRequest.Responded";
    public const string AdminParticipationDocumentRequestsListed = "Admin.ParticipationDocumentRequestsListed";
    public const string AdminParticipationDocumentRequestViewed = "Admin.ParticipationDocumentRequestViewed";
    public const string BadgeUpdateRequestSubmitted = "BadgeUpdateRequest.Submitted";
    public const string BadgeUpdateRequestResponded = "BadgeUpdateRequest.Responded";
    public const string AdminBadgeUpdateRequestsListed = "Admin.BadgeUpdateRequestsListed";
    public const string AdminBadgeUpdateRequestViewed = "Admin.BadgeUpdateRequestViewed";
    public const string AppRequestCancelled = "AppRequest.Cancelled";

    // Seat reservations (D-175, gap doc G11 — Mockup page 7)
    public const string HallSeatLayoutUpdated = "HallSeatLayout.Updated";
    public const string SeatReservationCreated = "SeatReservation.Created";
    public const string SeatReservationReleased = "SeatReservation.Released";
    public const string SeatRowAdminReserved = "SeatReservation.RowAdminReserved";
    public const string SeatRowAdminReleased = "SeatReservation.RowAdminReleased";

    // Booking approval workflow (P2.2 / D-227 — FDS-005 §5.2).
    public const string BookingApproved = "Booking.Approved";
    public const string BookingRejected = "Booking.Rejected";
    public const string BookingCancelled = "Booking.Cancelled";

    // Flexible hall config + B2B/B2C business meetings (SIMF-FDS-013 / D-248).
    public const string HallPurposeChanged = "Hall.PurposeChanged";
    public const string MeetingTableCreated = "MeetingTable.Created";
    public const string MeetingTableUpdated = "MeetingTable.Updated";
    public const string MeetingTableDeactivated = "MeetingTable.Deactivated";
    public const string MeetingTablesGenerated = "MeetingTable.Generated";
    public const string HallAllocationCreated = "HallAllocation.Created";
    public const string HallAllocationReleased = "HallAllocation.Released";
    public const string BusinessMeetingScheduled = "BusinessMeeting.Scheduled";
    public const string BusinessMeetingCancelled = "BusinessMeeting.Cancelled";

    // Shared contact directory (SIMF-FDS-014 / D-261).
    public const string ContactCreated = "Contact.Created";
    public const string ContactUpdated = "Contact.Updated";
    public const string ContactDeactivated = "Contact.Deactivated";

    // Centralised AI module (D-176, gap doc G12)
    public const string AiPromptCreated = "AiPrompt.Created";
    public const string AiPromptUpdated = "AiPrompt.Updated";
    public const string AiPromptDeactivated = "AiPrompt.Deactivated";
    public const string AiInvocationSucceeded = "AiInvocation.Succeeded";
    public const string AiInvocationFailed = "AiInvocation.Failed";

    // D-179 (review-pass) — admin drill-down on an invocation. SOC sees
    // admin-on-admin surveillance: "admin reads 50k invocations on Sunday
    // night" is otherwise invisible.
    public const string AiInvocationViewed = "AiInvocation.Viewed";

    // Invitations + VIP notify (D-168, gap doc G5 — public-relations
    // module, PDF §2.7.3)
    public const string InvitationCreated = "Invitation.Created";
    public const string InvitationUpdated = "Invitation.Updated";
    public const string InvitationStateChanged = "Invitation.StateChanged";
    public const string InvitationDeactivated = "Invitation.Deactivated";
    public const string VipNotificationSent = "Vip.NotificationSent";

    // Operations toggles (D-166, gap doc G4 — registration gate +
    // archive visibility singletons)
    public const string RegistrationGateUpdated = "RegistrationGate.Updated";
    public const string RegistrationGateAutoClosed = "RegistrationGate.AutoClosed";
    public const string ArchiveVisibilityUpdated = "ArchiveVisibility.Updated";
    public const string SignUpRejectedRegistrationClosed = "SignUp.RejectedRegistrationClosed";

    // Gates (D-148 — Gate Module, SIMF-FDS-003 §5.6, SIMF-API-GATES-001)
    public const string GateCreated = "Gate.Created";
    public const string GateUpdated = "Gate.Updated";
    public const string GateDeactivated = "Gate.Deactivated";
    public const string GateAssignmentAdded = "Gate.AssignmentAdded";
    public const string GateAssignmentRevoked = "Gate.AssignmentRevoked";
    public const string GateScanDenied = "Gate.ScanDenied";
    public const string GateFailureCircuitOpened = "Gate.FailureCircuitOpened";
    public const string GateFailureCircuitClosed = "Gate.FailureCircuitClosed";

    // Approval workflow (P4 — Admin / Visitor; P7c added the Other pair)
    public const string AdminStaffApproved = "Admin.StaffApproved";
    public const string AdminStaffRejected = "Admin.StaffRejected";
    public const string AdminOtherApproved = "Admin.OtherApproved";
    public const string AdminOtherRejected = "Admin.OtherRejected";
    public const string AdminVisitorApproved = "Admin.VisitorApproved";
    public const string AdminVisitorRejected = "Admin.VisitorRejected";

    // D-186 review-pass (threat-detection H-2) — fires from
    // AdminAccountService.LoadPendingSubjectAsync when an actor calls
    // an approval endpoint with a subject id from the wrong scope
    // (audience id on /admin/others/* or vice versa). The endpoint
    // returns 404 (same shape as not-found) so the probing actor
    // cannot tell scope-mismatch from missing-id; this audit row is
    // the only SOC visibility into the probe pattern.
    public const string AdminApprovalScopeMismatch = "Admin.ApprovalScopeMismatch";

    // Exhibitors + account provisioning (D-202 — D-199 #3).
    public const string ExhibitorCreated = "Exhibitor.Created";
    public const string ExhibitorUpdated = "Exhibitor.Updated";
    public const string ExhibitorDeactivated = "Exhibitor.Deactivated";
    public const string ExhibitorAccountProvisioned = "Exhibitor.AccountProvisioned";

    // News (D-199 — PR / marketing news. Promoted from AdminNewsService
    // module-local consts; string values are the audit contract and must
    // stay verbatim).
    public const string NewsCreated = "news.created";
    public const string NewsUpdated = "news.updated";
    public const string NewsDeactivated = "news.deactivated";

    // FAQ (P2.1 / D-211 — two-level group → entry CRUD).
    public const string FaqGroupCreated = "faq.group.created";
    public const string FaqGroupUpdated = "faq.group.updated";
    public const string FaqGroupDeactivated = "faq.group.deactivated";
    public const string FaqEntryCreated = "faq.entry.created";
    public const string FaqEntryUpdated = "faq.entry.updated";
    public const string FaqEntryDeactivated = "faq.entry.deactivated";

    // Media (D-199 — media gallery, Mockup page 30. Promoted from the
    // module-local MediaAuditEvents; string values are the audit contract).
    public const string MediaCreated = "admin.media.created";
    public const string MediaUpdated = "admin.media.updated";
    public const string MediaDeactivated = "admin.media.deactivated";
    public const string MediaImageSet = "admin.media.image.set";

    // Media partners (D-199, Mockup page 31. Promoted from the
    // AdminMediaPartnerService module-local consts; string values are the
    // audit contract).
    public const string MediaPartnerCreated = "MediaPartnerCreated";
    public const string MediaPartnerUpdated = "MediaPartnerUpdated";
    public const string MediaPartnerDeactivated = "MediaPartnerDeactivated";

    // Archive (D-199 — past editions, Mockup screen 24. Promoted from
    // AdminArchiveService module-local consts; string values are the audit
    // contract).
    public const string ArchiveEditionCreated = "archive_edition.created";
    public const string ArchiveEditionUpdated = "archive_edition.updated";
    public const string ArchiveEditionDeactivated = "archive_edition.deactivated";

    // Ratings — attendee submission (now upserts a RatingResponse with
    // per-question answers; string values are the audit contract).
    public const string RatingSubmitted = "Rating.Submitted";
    public const string RatingRevised = "Rating.Revised";

    // Rating configuration — admin CRUD over types / question groups / questions.
    public const string RatingTypeCreated = "RatingType.Created";
    public const string RatingTypeUpdated = "RatingType.Updated";
    public const string RatingTypeDeactivated = "RatingType.Deactivated";
    public const string RatingGroupCreated = "RatingGroup.Created";
    public const string RatingGroupUpdated = "RatingGroup.Updated";
    public const string RatingGroupDeactivated = "RatingGroup.Deactivated";
    public const string RatingQuestionCreated = "RatingQuestion.Created";
    public const string RatingQuestionUpdated = "RatingQuestion.Updated";
    public const string RatingQuestionDeactivated = "RatingQuestion.Deactivated";

    // Organisations (B3 / D-220 — Saudi-companies lookup + government Excel
    // bulk-import; one Imported row per upload carries the counts).
    public const string OrganisationCreated = "organisation.created";
    public const string OrganisationUpdated = "organisation.updated";
    public const string OrganisationDeactivated = "organisation.deactivated";
    public const string OrganisationImported = "organisation.imported";

    // Regions (administrative-regions lookup; the app region picker reads this table).
    public const string RegionCreated = "region.created";
    public const string RegionUpdated = "region.updated";
    public const string RegionDeactivated = "region.deactivated";
}
