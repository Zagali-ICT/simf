using System.ComponentModel.DataAnnotations;
using SIMF.Common.Resources.Enums;

namespace SIMF.Common.Enums;

/// <summary>
/// The stable kind of an in-app notification (P12 — D-053). Persisted
/// as the enum name string (e.g. <c>"AccountApproved"</c>) via the EF
/// value converter on <c>NotificationConfiguration</c>.
/// </summary>
public enum NotificationKind
{
    /// <summary>Dispatched after the initial verification email is queued (D-099).</summary>
    [Display(Description = nameof(ResNotificationKind.CredentialEmailVerificationSent), ResourceType = typeof(ResNotificationKind))]
    CredentialEmailVerificationSent = 0,

    /// <summary>Dispatched after a re-issued verification email is queued (D-099).</summary>
    [Display(Description = nameof(ResNotificationKind.CredentialEmailVerificationResent), ResourceType = typeof(ResNotificationKind))]
    CredentialEmailVerificationResent = 1,

    /// <summary>Dispatched after the sign-in OTP email is queued (D-099, email-OTP branch only).</summary>
    [Display(Description = nameof(ResNotificationKind.CredentialSignInOtpSent), ResourceType = typeof(ResNotificationKind))]
    CredentialSignInOtpSent = 2,

    /// <summary>Dispatched after the password-reset email is queued (D-099).</summary>
    [Display(Description = nameof(ResNotificationKind.CredentialPasswordResetRequested), ResourceType = typeof(ResNotificationKind))]
    CredentialPasswordResetRequested = 3,

    /// <summary>Dispatched after the first profile save auto-transitions the user to PendingApproval (H2 — D-057).</summary>
    [Display(Description = nameof(ResNotificationKind.AccountProfileSubmitted), ResourceType = typeof(ResNotificationKind))]
    AccountProfileSubmitted = 10,

    /// <summary>Dispatched to every Administrator when a new visitor becomes pending approval (P13 — D-054).</summary>
    [Display(Description = nameof(ResNotificationKind.AdminPendingVisitor), ResourceType = typeof(ResNotificationKind))]
    AdminPendingVisitor = 11,

    /// <summary>Dispatched when an account is approved and the QR id is minted (P13 — D-054).</summary>
    [Display(Description = nameof(ResNotificationKind.AccountApproved), ResourceType = typeof(ResNotificationKind))]
    AccountApproved = 12,

    /// <summary>Dispatched when an account is rejected (P13 — D-054).</summary>
    [Display(Description = nameof(ResNotificationKind.AccountRejected), ResourceType = typeof(ResNotificationKind))]
    AccountRejected = 13,

    /// <summary>Dispatched when an administrator clears the subject's 2FA (P13 — D-054).</summary>
    [Display(Description = nameof(ResNotificationKind.AccountTwoFactorReset), ResourceType = typeof(ResNotificationKind))]
    AccountTwoFactorReset = 14,

    /// <summary>D-111: dispatched on email-verification success OR when an
    /// administrator creates the account — the first warm contact the user
    /// has with SIMF after their identity is proved.</summary>
    [Display(Description = nameof(ResNotificationKind.AccountWelcome), ResourceType = typeof(ResNotificationKind))]
    AccountWelcome = 20,

    /// <summary>D-111: dispatched when the user successfully changes their
    /// own password. Security trail visible to the account holder.</summary>
    [Display(Description = nameof(ResNotificationKind.AccountPasswordChanged), ResourceType = typeof(ResNotificationKind))]
    AccountPasswordChanged = 21,

    /// <summary>D-111: dispatched when a forgot-password reset completes
    /// successfully. Mirrors the bank-style "your password was reset"
    /// security notice.</summary>
    [Display(Description = nameof(ResNotificationKind.AccountPasswordResetCompleted), ResourceType = typeof(ResNotificationKind))]
    AccountPasswordResetCompleted = 22,

    /// <summary>D-112: dispatched to every Approved Administrator (except
    /// the actor) when a new account lands in PendingApproval — covers
    /// both the admin-create path AND any future automated-create path.
    /// Distinct from <see cref="AdminPendingVisitor"/> which fires
    /// specifically on visitor self-submit.</summary>
    [Display(Description = nameof(ResNotificationKind.AdminPendingApproval), ResourceType = typeof(ResNotificationKind))]
    AdminPendingApproval = 23,

    /// <summary>D-168 (gap doc G5): dispatched to a recipient when the PR
    /// team creates an invitation row for them. In-app row only by
    /// default — the PR rep can opt-in to a follow-up email via the
    /// "Notify VIPs" desk.</summary>
    [Display(Description = nameof(ResNotificationKind.InvitationReceived), ResourceType = typeof(ResNotificationKind))]
    InvitationReceived = 30,

    /// <summary>D-168 (gap doc G5): dispatched to one or more VIPs by the
    /// PR team via the "Notify VIPs" desk. Body is the rep's free-text
    /// message; sent as an in-app row + a queued email.</summary>
    [Display(Description = nameof(ResNotificationKind.VipBroadcast), ResourceType = typeof(ResNotificationKind))]
    VipBroadcast = 31,

    /// <summary>P1.7: dispatched to a visitor when their seat reservation for a
    /// session is confirmed (self-pick or random allocation). In-app row only
    /// — a low-stakes confirmation, no email.</summary>
    [Display(Description = nameof(ResNotificationKind.BookingConfirmed), ResourceType = typeof(ResNotificationKind))]
    BookingConfirmed = 40,
}
