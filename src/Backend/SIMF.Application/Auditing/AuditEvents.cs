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
    // D-206: a Control Panel sign-in with a forced-change credential was handed a
    // single-use password-change ticket (in place of the 403). The completion is
    // audited as PasswordChanged, like any other password change.
    public const string SignInPasswordChangeTicketIssued = "SignIn.PasswordChangeTicketIssued";
    public const string SignInWrongSurface = "SignIn.WrongSurface";
    public const string SignInSecondFactorIssued = "SignIn.SecondFactorIssued";
    public const string SignInSecondFactorFailed = "SignIn.SecondFactorFailed";
    public const string SignInSecondFactorRejected = "SignIn.SecondFactorRejected";
    public const string SignInSucceeded = "SignIn.Succeeded";
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

    // User profile (myComment #18 — D-046; P8 renamed from VisitorProfile.*)
    public const string UserProfileSaved = "UserProfile.Saved";
    public const string UserProfileIdImageUploaded = "UserProfile.IdImageUploaded";
    public const string UserProfileIdImageRejected = "UserProfile.IdImageRejected";

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

    // Audience comments (D-199, Mockup page 28 — public submit + AI-filter
    // landing state + admin moderation; distinct from SessionQuestion)
    public const string SessionCommentSubmitted = "SessionComment.Submitted";
    public const string SessionCommentApproved = "SessionComment.Approved";
    public const string SessionCommentHidden = "SessionComment.Hidden";
    public const string SessionCommentRepended = "SessionComment.Repended";
    public const string SessionCommentDeactivated = "SessionComment.Deactivated";

    // Device keys / biometric sign-in (D-172, gap doc G10 — PDF §2.5)
    public const string DeviceKeyRegistered = "DeviceKey.Registered";
    public const string DeviceKeyRevoked = "DeviceKey.Revoked";
    public const string SignInWithDeviceKey = "SignIn.WithDeviceKey";
    public const string SignInWithDeviceKeyFailed = "SignIn.WithDeviceKeyFailed";

    // CMS: ContentBlock + Banner (D-173, gap doc G8 — PDF §1, §2.1)
    public const string ContentBlockUpserted = "ContentBlock.Upserted";
    public const string ContentBlockDeactivated = "ContentBlock.Deactivated";
    public const string BannerCreated = "Banner.Created";
    public const string BannerUpdated = "Banner.Updated";
    public const string BannerDeactivated = "Banner.Deactivated";

    // Delegations + MeetingRequests (D-174, gap doc G11 — Mockup pages 21 + 27)
    public const string DelegationCreated = "Delegation.Created";
    public const string DelegationUpdated = "Delegation.Updated";
    public const string DelegationDeactivated = "Delegation.Deactivated";
    public const string MeetingRequestSubmitted = "MeetingRequest.Submitted";
    public const string MeetingRequestResponded = "MeetingRequest.Responded";
    // D-185 — security carry-over from D-184: admin queries the queue.
    // SOC needs to see "admin pulled the full meeting-request list at
    // 03:00 on a weekend" because the list response carries requester
    // names (and the per-row detail click reveals emails).
    public const string AdminMeetingRequestsListed = "Admin.MeetingRequestsListed";
    // D-185 — admin drilled into one meeting request (fetched the
    // detail record, including the requester's email). Naming pinned
    // to the Admin.* prefix matching AdminMeetingRequestsListed so SOC
    // rule authors filtering on `EventType startswith 'Admin.'` see
    // both events together.
    public const string AdminMeetingRequestViewed = "Admin.MeetingRequestViewed";

    // Seat reservations (D-175, gap doc G11 — Mockup page 7)
    public const string HallSeatLayoutUpdated = "HallSeatLayout.Updated";
    public const string SeatReservationCreated = "SeatReservation.Created";
    public const string SeatReservationReleased = "SeatReservation.Released";
    public const string SeatRowAdminReserved = "SeatReservation.RowAdminReserved";
    public const string SeatRowAdminReleased = "SeatReservation.RowAdminReleased";

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

    // Companies + exhibitor/sponsor account provisioning (D-202 — D-199 #3).
    public const string CompanyCreated = "Company.Created";
    public const string CompanyUpdated = "Company.Updated";
    public const string CompanyDeactivated = "Company.Deactivated";
    public const string CompanyAccountProvisioned = "Company.AccountProvisioned";

    // News (D-199 — PR / marketing news. Promoted from AdminNewsService
    // module-local consts; string values are the audit contract and must
    // stay verbatim).
    public const string NewsCreated = "news.created";
    public const string NewsUpdated = "news.updated";
    public const string NewsDeactivated = "news.deactivated";

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

    // Ratings (D-199 — "Rate the Forum", Mockup screen 40. Promoted from the
    // RatingService module-local consts; string values are the audit
    // contract).
    public const string RatingSubmitted = "Rating.Submitted";
    public const string RatingRevised = "Rating.Revised";
}
