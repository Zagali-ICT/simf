namespace SIMF.Application.Auditing;

/// <summary>
/// The stable audit event-type names. Names are
/// defined here once and never written as literals elsewhere.
/// </summary>
public static class AuditEvents
{
    public const string SignUpSucceeded = "SignUp.Succeeded";
    public const string SignUpDuplicateEmail = "SignUp.DuplicateEmail";
    // Re-sign-up of an unverified account: password replaced + a
    // fresh verification code issued (enumeration-resistant sign-up).
    public const string SignUpRestartedUnverified = "SignUp.RestartedUnverified";
    // Sign-up attempt against an existing *verified* account: the
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
    // NCA — sign-in found the password older than the configured max age and
    // forced a change. Distinct from a seeded/admin-rotated forced change so SOC
    // can see expiry-driven rotations.
    public const string SignInPasswordExpired = "SignIn.PasswordExpired";
    // A Control Panel sign-in with a forced-change credential was handed a
    // single-use password-change ticket (in place of the 403). The completion is
    // audited as PasswordChanged, like any other password change.
    public const string SignInPasswordChangeTicketIssued = "SignIn.PasswordChangeTicketIssued";
    // A Control Panel sign-in proved its password but the
    // account carries no authenticator secret, so NO token was issued — the
    // caller was handed a single-use mandatory-enrolment ticket instead. The
    // completion is audited as TotpEnrolmentConfirmed + SignInSucceeded, like
    // any other enrolment and sign-in.
    public const string SignInTwoFactorEnrolmentRequired = "SignIn.TwoFactorEnrolmentRequired";
    public const string SignInWrongSurface = "SignIn.WrongSurface";
    public const string SignInSecondFactorIssued = "SignIn.SecondFactorIssued";
    public const string SignInSecondFactorFailed = "SignIn.SecondFactorFailed";
    public const string SignInSecondFactorRejected = "SignIn.SecondFactorRejected";
    public const string SignInSucceeded = "SignIn.Succeeded";
    // Badge-QR activation (a passwordless account sets its first password).
    public const string BadgeActivationStarted = "BadgeActivation.Started";
    public const string BadgeActivationCompleted = "BadgeActivation.Completed";
    public const string BadgeActivationFailed = "BadgeActivation.Failed";
    // A non-approved user signed in
    // (EmailVerified, PendingApproval or Rejected). They got tokens +
    // AccountStateInfo; routed to the profile-form / state-banner page by
    // the front-end. SOC needs to spot these.
    public const string SignInAsGuest = "SignIn.AsGuest";
    public const string RefreshTokenIssued = "RefreshToken.Issued";
    public const string RefreshTokenRotated = "RefreshToken.Rotated";
    public const string RefreshTokenReused = "RefreshToken.Reused";
    public const string RefreshTokenRejected = "RefreshToken.Rejected";
    public const string AccessTokenRejected = "AccessToken.Rejected";
    // NCA Secure Application-Development Standard — an authenticated
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

    // The configured SuperAdmin:Email did not match any account while OTHER
    // Administrator accounts already existed, so seeding created a second
    // wildcard-privilege account beside them instead of moving the first. Almost
    // always a changed SuperAdmin:Email against an existing database.
    public const string SuperAdminDuplicateSeeded = "Admin.SuperAdminDuplicateSeeded";
    // NCA — the daily sweep disabled an account for inactivity beyond the
    // configured threshold. A system action (no actor).
    public const string AccountDormantDisabled = "Account.DormantDisabled";

    // An email-enqueue failure that lands AFTER the matching
    // code row is already persisted to the DB. The success audit was
    // already written; this row tells SOC the user never actually got
    // the message even though everything else looked clean.
    public const string EmailEnqueueFailed = "Email.EnqueueFailed";

    // TOTP enrolment
    public const string TotpEnrolmentStarted = "Totp.EnrolmentStarted";
    public const string TotpEnrolmentConfirmed = "Totp.EnrolmentConfirmed";
    public const string TotpEnrolmentFailed = "Totp.EnrolmentFailed";
    public const string TotpDisabled = "Totp.Disabled";
    public const string TotpDisableFailed = "Totp.DisableFailed";
    public const string TotpRecoveryCodesGenerated = "Totp.RecoveryCodesGenerated";
    public const string TotpRecoveryCodesRegenerated = "Totp.RecoveryCodesRegenerated";
    public const string TotpRecoveryCodeUsed = "Totp.RecoveryCodeUsed";
    public const string TotpRecoveryCodeFailed = "Totp.RecoveryCodeFailed";

    // Avatar
    public const string AvatarUpdated = "Avatar.Updated";
    public const string AvatarRejected = "Avatar.Rejected";

    // Admin-driven user management
    public const string AdminTwoFactorReset = "Admin.TwoFactorReset";
    public const string AdminTwoFactorResetFailed = "Admin.TwoFactorResetFailed";
    public const string AdminUserCreated = "Admin.UserCreated";
    public const string AdminUserCreateFailed = "Admin.UserCreateFailed";
    // Admin edit of a Visitor / Other account.
    public const string AdminUserUpdated = "Admin.UserUpdated";
    public const string AdminUserUpdateFailed = "Admin.UserUpdateFailed";
    // Admin flip of an account between the audience
    // (Visitor) and partner (Other) scope. Failures reuse AdminUserUpdateFailed.
    public const string AdminUserTypeChanged = "Admin.UserTypeChanged";

    // Admin-driven bulk actions
    public const string AdminUserDeleted = "Admin.UserDeleted";
    public const string AdminUserDeleteFailed = "Admin.UserDeleteFailed";
    public const string AdminUserDuplicated = "Admin.UserDuplicated";
    public const string AdminUsersExported = "Admin.UsersExported";
    public const string AdminUsersImported = "Admin.UsersImported";

    // XLSX export of the read-only admin grids (filtered result set).
    public const string AdminOperationLogExported = "Admin.OperationLogExported";
    public const string AdminAttendeesExported = "Admin.AttendeesExported";

    // User profile
    public const string UserProfileSaved = "UserProfile.Saved";
    public const string UserProfileIdImageUploaded = "UserProfile.IdImageUploaded";
    public const string UserProfileIdImageRejected = "UserProfile.IdImageRejected";
    // PII — every admin READ/disclosure of a subject's ID-document image.
    public const string UserProfileIdImageViewed = "UserProfile.IdImageViewed";
    // VVIP/VIP welcome photo (موج) upload.
    public const string UserProfileVipPhotoUploaded = "UserProfile.VipPhotoUploaded";
    // PII — every admin READ/disclosure of a subject's VIP photo.
    public const string UserProfileVipPhotoViewed = "UserProfile.VipPhotoViewed";

    // Interests (الاهتمامات)
    public const string InterestCreated = "Interest.Created";
    public const string InterestUpdated = "Interest.Updated";
    public const string InterestDeactivated = "Interest.Deactivated";

    // ProfileTypes admin CRUD
    public const string ProfileTypeCreated = "ProfileType.Created";
    public const string ProfileTypeUpdated = "ProfileType.Updated";
    public const string ProfileTypeDeactivated = "ProfileType.Deactivated";

    // On-site walk-in registration desk
    public const string AdminWalkInRegistered = "Admin.WalkInRegistered";
    public const string AdminWalkInRegisterFailed = "Admin.WalkInRegisterFailed";

    /// <summary>A walk-in that skipped the approval queue because the
    /// walk-in mode was armed. Written IN ADDITION TO
    /// <see cref="AdminWalkInRegistered"/>, never instead of it, so an auditor
    /// can diff "all walk-ins" against "walk-ins that skipped review" from one
    /// table. This is the post-event review list.</summary>
    public const string AdminVisitorAutoApproved = "Admin.VisitorAutoApproved";

    /// <summary>Auto-approval failed, so the account stayed
    /// PendingApproval. The registration itself survived; the desk falls back to
    /// a paper slip and an admin approves from the normal queue.</summary>
    public const string AdminVisitorAutoApproveFailed = "Admin.VisitorAutoApproveFailed";

    /// <summary>A walk-in registered with the reduced quick field set.
    /// Detail names the fields that were omitted so they can be chased and
    /// completed after the event.</summary>
    public const string AdminQuickRegistered = "Admin.QuickRegistered";

    /// <summary>One reconciliation upload from an offline badge desk.
    /// Detail carries the per-batch tallies (submitted / created / pending /
    /// already-uploaded / rejected), which is the reconciliation report: an
    /// auditor can add these up and compare against the badges printed.</summary>
    public const string AdminOfflineBadgeBatchUploaded = "Admin.OfflineBadgeBatchUploaded";
    // Bulk-generate placeholder badges (by profile type + count).
    public const string AdminBulkBadgesGenerated = "Admin.BulkBadgesGenerated";

    // Logs — per-project log files + the CP viewer
    public const string AdminLogViewed = "Admin.LogViewed";
    public const string AdminLogDownloaded = "Admin.LogDownloaded";

    // Roles — admin CRUD over the existing SimfRole / RolePermission /
    // Permission entities; no schema change.
    public const string RoleCreated = "Role.Created";
    public const string RoleUpdated = "Role.Updated";
    public const string RoleDeleted = "Role.Deleted";
    // An admin changed a custom role's permission grants.
    public const string RolePermissionsUpdated = "Role.PermissionsUpdated";
    // An admin changed which roles a user holds.
    public const string UserRolesUpdated = "User.RolesUpdated";

    // Programme themes
    public const string ThemeCreated = "Theme.Created";
    public const string ThemeUpdated = "Theme.Updated";
    public const string ThemeDeactivated = "Theme.Deactivated";

    // Venue halls
    public const string HallCreated = "Hall.Created";
    public const string HallUpdated = "Hall.Updated";
    public const string HallDeactivated = "Hall.Deactivated";

    // The admin-managed country lookup
    public const string CountryCreated = "Country.Created";
    public const string CountryUpdated = "Country.Updated";
    public const string CountryDeactivated = "Country.Deactivated";

    // Programme speakers
    public const string SpeakerCreated = "Speaker.Created";
    public const string SpeakerUpdated = "Speaker.Updated";
    public const string SpeakerDeactivated = "Speaker.Deactivated";

    // Speaker presentations
    public const string SpeakerPresentationUploaded = "SpeakerPresentation.Uploaded";
    public const string SpeakerPresentationDeleted = "SpeakerPresentation.Deleted";

    // Unified media assets — the one upload/download pipeline for speaker
    // photos, company / sponsor / media-partner logos, archive covers, news images)
    public const string AssetUploaded = "Asset.Uploaded";
    public const string AssetLinked = "Asset.Linked";
    public const string AssetRemoved = "Asset.Removed";
    public const string AssetRestored = "Asset.Restored";

    // The centralized file store. Every file action is audited, including
    // a denied private download (SAMA E-16/17 / NCA ECC 2-12). Public-file reads
    // are not audited per-row (they would flood the log).
    public const string FileUploaded = "File.Uploaded";
    public const string FileLinked = "File.Linked";
    public const string FileDownloaded = "File.Downloaded";
    public const string FileAccessDenied = "File.AccessDenied";
    public const string FileIntegrityFailed = "File.IntegrityFailed";
    public const string FileDeleted = "File.Deleted";
    public const string FileSecurelyDestroyed = "File.SecurelyDestroyed";

    // System configuration settings
    public const string SystemSettingCreated = "SystemSetting.Created";
    public const string SystemSettingUpdated = "SystemSetting.Updated";
    public const string SystemSettingDeactivated = "SystemSetting.Deactivated";

    // Organization / About profile
    public const string OrganizationProfileUpdated = "OrganizationProfile.Updated";

    // Venue map nodes
    public const string VenueMapNodeCreated = "VenueMapNode.Created";
    public const string VenueMapNodeUpdated = "VenueMapNode.Updated";
    public const string VenueMapNodeDeactivated = "VenueMapNode.Deactivated";

    // Booths — the Exhibition module plus the 2D venue map.
    public const string BoothCreated = "Booth.Created";
    public const string BoothUpdated = "Booth.Updated";
    public const string BoothDeactivated = "Booth.Deactivated";

    // Event sponsors
    public const string SponsorCreated = "Sponsor.Created";
    public const string SponsorUpdated = "Sponsor.Updated";
    public const string SponsorDeactivated = "Sponsor.Deactivated";

    // Programme sessions
    public const string SessionCreated = "Session.Created";
    public const string SessionUpdated = "Session.Updated";
    public const string SessionDeactivated = "Session.Deactivated";
    // Broadcast-lifecycle transitions.
    public const string SessionStatusChanged = "Session.StatusChanged";
    public const string SessionPublished = "Session.Published";
    public const string SessionUnpublished = "Session.Unpublished";
    // Session-recording attach / delete.
    public const string SessionRecordingUploaded = "Session.RecordingUploaded";
    public const string SessionRecordingDeleted = "Session.RecordingDeleted";
    // Hall arrival / departure (geofence or QR door scan).
    public const string HallArrivalRecorded = "HallAttendance.ArrivalRecorded";
    public const string HallDepartureRecorded = "HallAttendance.DepartureRecorded";
    // AI session-summary / محضر committee actions.
    public const string SessionSummaryGenerated = "SessionSummary.Generated";
    public const string SessionSummarySaved = "SessionSummary.Saved";
    public const string SessionSummaryPublished = "SessionSummary.Published";
    public const string SessionSummaryUnpublished = "SessionSummary.Unpublished";
    // The team review/approval workflow on the محضر.
    public const string SessionSummarySubmittedForReview = "SessionSummary.SubmittedForReview";
    public const string SessionSummaryApproved = "SessionSummary.Approved";
    public const string SessionSummaryReturnedToDraft = "SessionSummary.ReturnedToDraft";

    // Session questions + moderator grants
    public const string SessionQuestionSubmitted = "SessionQuestion.Submitted";
    public const string SessionQuestionHidden = "SessionQuestion.Hidden";
    public const string SessionQuestionUnhidden = "SessionQuestion.Unhidden";
    public const string SessionQuestionPushed = "SessionQuestion.Pushed";
    // The moderator's "تمت الإجابة" mark, persisted.
    public const string SessionQuestionAnswered = "SessionQuestion.Answered";
    public const string SessionQuestionUnanswered = "SessionQuestion.Unanswered";
    public const string SessionQuestionReordered = "SessionQuestion.Reordered";
    public const string SessionModeratorAssigned = "SessionModerator.Assigned";
    public const string SessionModeratorRevoked = "SessionModerator.Revoked";

    // Venue self-assert
    public const string SessionQuestionRejectedNotAtVenue = "SessionQuestion.RejectedNotAtVenue";
    // Scientific-Committee pipeline actions.
    public const string SessionQuestionApproved = "SessionQuestion.Approved";
    public const string SessionQuestionEscalated = "SessionQuestion.Escalated";

    // Networking connections — visitor-to-visitor request / accept.
    public const string ConnectionRequested = "Connection.Requested";
    public const string ConnectionAccepted = "Connection.Accepted";
    public const string ConnectionRemoved = "Connection.Removed";

    // Session categories — a team-seeded lookup, not a fixed enum.
    public const string SessionCategoryCreated = "SessionCategory.Created";
    public const string SessionCategoryUpdated = "SessionCategory.Updated";
    public const string SessionCategoryDeactivated = "SessionCategory.Deactivated";

    // Programme days (date + title + logo).
    public const string ProgrammeDayCreated = "ProgrammeDay.Created";
    public const string ProgrammeDayUpdated = "ProgrammeDay.Updated";
    public const string ProgrammeDayDeactivated = "ProgrammeDay.Deactivated";

    // Device keys / biometric sign-in
    public const string DeviceKeyRegistered = "DeviceKey.Registered";
    public const string DeviceKeyRevoked = "DeviceKey.Revoked";
    public const string SignInWithDeviceKey = "SignIn.WithDeviceKey";
    public const string SignInWithDeviceKeyFailed = "SignIn.WithDeviceKeyFailed";
    // Emailed-OTP step-up guarding biometric device-key enrolment.
    public const string DeviceKeyStepUpIssued = "DeviceKey.StepUpIssued";
    public const string DeviceKeyStepUpRejected = "DeviceKey.StepUpRejected";

    // CMS: ContentBlock + Banner
    public const string ContentBlockUpserted = "ContentBlock.Upserted";
    public const string ContentBlockDeactivated = "ContentBlock.Deactivated";
    public const string BannerCreated = "Banner.Created";
    public const string BannerUpdated = "Banner.Updated";
    public const string BannerDeactivated = "Banner.Deactivated";

    // Speaker meeting requests. Same SOC rationale as
    // the session-scoped events above: the list carries requester names and the
    // per-record detail/respond reveals the requester email.
    public const string SpeakerMeetingRequestSubmitted = "SpeakerMeetingRequest.Submitted";
    public const string SpeakerMeetingRequestResponded = "SpeakerMeetingRequest.Responded";
    // The AwaitingSpeaker->Pending auto-revert (worker) + the admin re-send of the
    // speaker confirmation links.
    public const string SpeakerMeetingRequestReverted = "SpeakerMeetingRequest.Reverted";
    public const string SpeakerMeetingConfirmationResent = "SpeakerMeetingRequest.ConfirmationResent";
    // An admin reopens a Rejected / Cancelled request back to Pending so a
    // mistaken decline or cancel is recoverable.
    public const string SpeakerMeetingRequestReopened = "SpeakerMeetingRequest.Reopened";
    // Speaker availability windows for the VIP-meeting slots.
    public const string SpeakerAvailabilityWindowCreated = "SpeakerAvailabilityWindow.Created";
    public const string SpeakerAvailabilityWindowDeleted = "SpeakerAvailabilityWindow.Deleted";
    // Hall availability windows.
    public const string HallAvailabilityWindowCreated = "HallAvailabilityWindow.Created";
    public const string HallAvailabilityWindowDeleted = "HallAvailabilityWindow.Deleted";
    // Delegation availability windows (parity with speaker).
    public const string DelegationAvailabilityWindowCreated = "DelegationAvailabilityWindow.Created";
    public const string DelegationAvailabilityWindowDeleted = "DelegationAvailabilityWindow.Deleted";
    // Operator hall check-in flips a confirmed meeting to Done.
    public const string SpeakerMeetingRequestCheckedIn = "SpeakerMeetingRequest.CheckedIn";
    public const string DelegationMeetingRequestCheckedIn = "DelegationMeetingRequest.CheckedIn";
    // Delegation↔delegation (G2G) meeting requests.
    public const string DelegationMeetingRequestSubmitted = "DelegationMeetingRequest.Submitted";
    public const string DelegationMeetingRequestResponded = "DelegationMeetingRequest.Responded";
    // The AwaitingSpeaker->Pending auto-revert for a delegation meeting whose
    // confirm token expired unused (the delegation twin of SpeakerMeetingRequestReverted).
    public const string DelegationMeetingRequestReverted = "DelegationMeetingRequest.Reverted";
    public const string AdminDelegationMeetingRequestsListed = "Admin.DelegationMeetingRequestsListed";
    public const string AdminDelegationMeetingRequestViewed = "Admin.DelegationMeetingRequestViewed";
    public const string AdminSpeakerMeetingRequestsListed = "Admin.SpeakerMeetingRequestsListed";
    public const string AdminSpeakerMeetingRequestViewed = "Admin.SpeakerMeetingRequestViewed";
    // The speaker double-opt-in action-link token: minted with the email,
    // previewed on link open, applied on confirm. One row per mint / click /
    // outcome, so the whole exchange is reconstructable.
    public const string MeetingActionTokenMinted = "MeetingActionToken.Minted";
    public const string MeetingActionTokenViewed = "MeetingActionToken.Viewed";
    public const string MeetingActionTokenApplied = "MeetingActionToken.Applied";

    // Participation-document + badge-update requests (الطلبات)
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

    // Seat reservations
    public const string HallSeatLayoutUpdated = "HallSeatLayout.Updated";
    // The whole grid was removed (the hall reverts to general admission).
    public const string HallSeatLayoutDeleted = "HallSeatLayout.Deleted";
    public const string SeatReservationCreated = "SeatReservation.Created";
    public const string SeatReservationReleased = "SeatReservation.Released";
    // A self-service seat CHANGE: one atomic release-and-re-hold, audited as a
    // single event carrying both the old and the new seat.
    public const string SeatReservationMoved = "SeatReservation.Moved";
    public const string SeatRowAdminReserved = "SeatReservation.RowAdminReserved";
    public const string SeatRowAdminReleased = "SeatReservation.RowAdminReleased";

    // Booking approval workflow.
    public const string BookingApproved = "Booking.Approved";
    public const string BookingRejected = "Booking.Rejected";
    public const string BookingCancelled = "Booking.Cancelled";

    // Flexible hall config + B2B/B2C business meetings.
    public const string HallPurposeChanged = "Hall.PurposeChanged";
    public const string MeetingTableCreated = "MeetingTable.Created";
    public const string MeetingTableUpdated = "MeetingTable.Updated";
    public const string MeetingTableDeactivated = "MeetingTable.Deactivated";
    public const string MeetingTablesGenerated = "MeetingTable.Generated";
    public const string HallAllocationCreated = "HallAllocation.Created";
    public const string HallAllocationReleased = "HallAllocation.Released";
    public const string BusinessMeetingScheduled = "BusinessMeeting.Scheduled";
    public const string BusinessMeetingCancelled = "BusinessMeeting.Cancelled";

    // The centralised AI module
    public const string AiPromptCreated = "AiPrompt.Created";
    public const string AiPromptUpdated = "AiPrompt.Updated";
    public const string AiPromptDeactivated = "AiPrompt.Deactivated";
    // Transactional email templates.
    public const string EmailTemplateUpdated = "EmailTemplate.Updated";
    public const string EmailTemplateReset = "EmailTemplate.Reset";
    public const string AiInvocationSucceeded = "AiInvocation.Succeeded";
    public const string AiInvocationFailed = "AiInvocation.Failed";

    // Admin drill-down on an invocation. SOC sees
    // admin-on-admin surveillance: "admin reads 50k invocations on Sunday
    // night" is otherwise invisible.
    public const string AiInvocationViewed = "AiInvocation.Viewed";

    // Invitations + VIP notify — the public-relations module.
    public const string InvitationCreated = "Invitation.Created";
    public const string InvitationUpdated = "Invitation.Updated";
    public const string InvitationStateChanged = "Invitation.StateChanged";
    public const string InvitationDeactivated = "Invitation.Deactivated";
    public const string VipNotificationSent = "Vip.NotificationSent";

    // Notification broadcasts (Control Panel "Announcements" desk).
    public const string BroadcastQueued = "Notification.BroadcastQueued";
    public const string BroadcastSent = "Notification.BroadcastSent";

    // Operations toggles — the registration-gate and archive-visibility
    // singletons.
    public const string RegistrationGateUpdated = "RegistrationGate.Updated";
    public const string RegistrationGateAutoClosed = "RegistrationGate.AutoClosed";
    public const string ArchiveVisibilityUpdated = "ArchiveVisibility.Updated";
    public const string SignUpRejectedRegistrationClosed = "SignUp.RejectedRegistrationClosed";

    // The Gate module
    public const string GateCreated = "Gate.Created";
    public const string GateUpdated = "Gate.Updated";
    public const string GateDeactivated = "Gate.Deactivated";
    public const string GateAssignmentAdded = "Gate.AssignmentAdded";
    public const string GateAssignmentRevoked = "Gate.AssignmentRevoked";
    public const string GateScanDenied = "Gate.ScanDenied";
    public const string GateFailureCircuitOpened = "Gate.FailureCircuitOpened";
    public const string GateFailureCircuitClosed = "Gate.FailureCircuitClosed";

    // Approval workflow — Admin / Visitor / Other
    public const string AdminStaffApproved = "Admin.StaffApproved";
    public const string AdminStaffRejected = "Admin.StaffRejected";
    public const string AdminOtherApproved = "Admin.OtherApproved";
    public const string AdminOtherRejected = "Admin.OtherRejected";
    public const string AdminVisitorApproved = "Admin.VisitorApproved";
    public const string AdminVisitorRejected = "Admin.VisitorRejected";

    // Threat detection — fires from
    // AdminAccountService.LoadPendingSubjectAsync when an actor calls
    // an approval endpoint with a subject id from the wrong scope
    // (audience id on /admin/others/* or vice versa). The endpoint
    // returns 404 (same shape as not-found) so the probing actor
    // cannot tell scope-mismatch from missing-id; this audit row is
    // the only SOC visibility into the probe pattern.
    public const string AdminApprovalScopeMismatch = "Admin.ApprovalScopeMismatch";

    // Exhibitors + account provisioning.
    public const string ExhibitorCreated = "Exhibitor.Created";
    public const string ExhibitorUpdated = "Exhibitor.Updated";
    public const string ExhibitorDeactivated = "Exhibitor.Deactivated";
    public const string ExhibitorAccountProvisioned = "Exhibitor.AccountProvisioned";

    // An EXISTING account (typically created through the generic Others
    // pipeline) attached to an exhibitor from the Control Panel. Distinct from
    // AccountProvisioned: no account is created here, an existing one gains the
    // booth membership that carries the lead-capture authority.
    public const string ExhibitorAccountLinked = "Exhibitor.AccountLinked";
    /// <summary>A booth officer dropped a captured lead from the
    /// booth's My Visitors list (soft-delete). Recorded because the row carries
    /// the visitor's consent trail: the capture notified the visitor that their
    /// card had been shared, so its removal has to be attributable too.</summary>
    public const string ExhibitorLeadRemoved = "Exhibitor.LeadRemoved";

    // News — PR / marketing. Promoted from AdminNewsService
    // module-local consts; string values are the audit contract and must
    // stay verbatim).
    public const string NewsCreated = "news.created";
    public const string NewsUpdated = "news.updated";
    public const string NewsDeactivated = "news.deactivated";

    // FAQ — two-level group → entry CRUD.
    public const string FaqGroupCreated = "faq.group.created";
    public const string FaqGroupUpdated = "faq.group.updated";
    public const string FaqGroupDeactivated = "faq.group.deactivated";
    public const string FaqEntryCreated = "faq.entry.created";
    public const string FaqEntryUpdated = "faq.entry.updated";
    public const string FaqEntryDeactivated = "faq.entry.deactivated";

    // The media gallery. Promoted from the
    // module-local MediaAuditEvents; string values are the audit contract).
    public const string MediaCreated = "admin.media.created";
    public const string MediaUpdated = "admin.media.updated";
    public const string MediaDeactivated = "admin.media.deactivated";
    public const string MediaImageSet = "admin.media.image.set";

    // Media partners. Promoted from the
    // AdminMediaPartnerService module-local consts; string values are the
    // audit contract).
    public const string MediaPartnerCreated = "MediaPartnerCreated";
    public const string MediaPartnerUpdated = "MediaPartnerUpdated";
    public const string MediaPartnerDeactivated = "MediaPartnerDeactivated";

    // Archive — past editions. Promoted from
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

    // Organisations — the Saudi-companies lookup + government Excel
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
