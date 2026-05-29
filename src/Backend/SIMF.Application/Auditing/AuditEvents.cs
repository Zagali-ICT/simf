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
    public const string SignInPasswordChangeRequired = "SignIn.PasswordChangeRequired";
    public const string SignInWrongSurface = "SignIn.WrongSurface";
    public const string SignInSecondFactorIssued = "SignIn.SecondFactorIssued";
    public const string SignInSecondFactorFailed = "SignIn.SecondFactorFailed";
    public const string SignInSecondFactorRejected = "SignIn.SecondFactorRejected";
    public const string SignInSucceeded = "SignIn.Succeeded";
    // P10 — D-051: a non-approved user signed in (PendingApproval or
    // Rejected). They got tokens + AccountStateInfo; routed to the
    // state-banner page by the front-end. SOC needs to spot these.
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

    // Admin-driven bulk actions (D-044 b)
    public const string AdminUserDeleted = "Admin.UserDeleted";
    public const string AdminUserDeleteFailed = "Admin.UserDeleteFailed";
    public const string AdminUserDuplicated = "Admin.UserDuplicated";
    public const string AdminUsersExported = "Admin.UsersExported";
    public const string AdminUsersImported = "Admin.UsersImported";

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

    // Sessions (D-165, gap doc G3 — programme sessions)
    public const string SessionCreated = "Session.Created";
    public const string SessionUpdated = "Session.Updated";
    public const string SessionDeactivated = "Session.Deactivated";

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
}
