using System.ComponentModel.DataAnnotations;
using SIMF.Common.Resources.Enums;

namespace SIMF.Common.Enums;

/// <summary>
/// The stable kind of an in-app notification. Persisted
/// as the enum name string (e.g. <c>"AccountApproved"</c>) via the EF
/// value converter on <c>NotificationConfiguration</c>.
/// </summary>
public enum NotificationKind
{
    /// <summary>Dispatched after the initial verification email is queued.</summary>
    [Display(Description = nameof(ResNotificationKind.CredentialEmailVerificationSent), ResourceType = typeof(ResNotificationKind))]
    CredentialEmailVerificationSent = 0,

    /// <summary>Dispatched after a re-issued verification email is queued.</summary>
    [Display(Description = nameof(ResNotificationKind.CredentialEmailVerificationResent), ResourceType = typeof(ResNotificationKind))]
    CredentialEmailVerificationResent = 1,

    /// <summary>Dispatched after the sign-in OTP email is queued (email-OTP branch only).</summary>
    [Display(Description = nameof(ResNotificationKind.CredentialSignInOtpSent), ResourceType = typeof(ResNotificationKind))]
    CredentialSignInOtpSent = 2,

    /// <summary>Dispatched after the password-reset email is queued.</summary>
    [Display(Description = nameof(ResNotificationKind.CredentialPasswordResetRequested), ResourceType = typeof(ResNotificationKind))]
    CredentialPasswordResetRequested = 3,

    /// <summary>Dispatched after the first profile save auto-transitions the
    /// user to PendingApproval.</summary>
    [Display(Description = nameof(ResNotificationKind.AccountProfileSubmitted), ResourceType = typeof(ResNotificationKind))]
    AccountProfileSubmitted = 10,

    /// <summary>Dispatched to every Administrator when a new visitor becomes pending approval.</summary>
    [Display(Description = nameof(ResNotificationKind.AdminPendingVisitor), ResourceType = typeof(ResNotificationKind))]
    AdminPendingVisitor = 11,

    /// <summary>Dispatched when an account is approved and the QR id is minted.</summary>
    [Display(Description = nameof(ResNotificationKind.AccountApproved), ResourceType = typeof(ResNotificationKind))]
    AccountApproved = 12,

    /// <summary>Dispatched when an account is rejected.</summary>
    [Display(Description = nameof(ResNotificationKind.AccountRejected), ResourceType = typeof(ResNotificationKind))]
    AccountRejected = 13,

    /// <summary>Dispatched when an administrator clears the subject's 2FA.</summary>
    [Display(Description = nameof(ResNotificationKind.AccountTwoFactorReset), ResourceType = typeof(ResNotificationKind))]
    AccountTwoFactorReset = 14,

    /// <summary>Dispatched on email-verification success OR when an
    /// administrator creates the account — the first warm contact the user
    /// has with SIMF after their identity is proved.</summary>
    [Display(Description = nameof(ResNotificationKind.AccountWelcome), ResourceType = typeof(ResNotificationKind))]
    AccountWelcome = 20,

    /// <summary>Dispatched when the user successfully changes their
    /// own password. Security trail visible to the account holder.</summary>
    [Display(Description = nameof(ResNotificationKind.AccountPasswordChanged), ResourceType = typeof(ResNotificationKind))]
    AccountPasswordChanged = 21,

    /// <summary>Dispatched when a forgot-password reset completes
    /// successfully. Mirrors the bank-style "your password was reset"
    /// security notice.</summary>
    [Display(Description = nameof(ResNotificationKind.AccountPasswordResetCompleted), ResourceType = typeof(ResNotificationKind))]
    AccountPasswordResetCompleted = 22,

    /// <summary>Dispatched to every Approved Administrator (except
    /// the actor) when a new account lands in PendingApproval — covers
    /// both the admin-create path AND any future automated-create path.
    /// Distinct from <see cref="AdminPendingVisitor"/> which fires
    /// specifically on visitor self-submit.</summary>
    [Display(Description = nameof(ResNotificationKind.AdminPendingApproval), ResourceType = typeof(ResNotificationKind))]
    AdminPendingApproval = 23,

    /// <summary>Dispatched to a recipient when the PR
    /// team creates an invitation row for them. In-app row only by
    /// default — the PR rep can opt-in to a follow-up email via the
    /// "Notify VIPs" desk.</summary>
    [Display(Description = nameof(ResNotificationKind.InvitationReceived), ResourceType = typeof(ResNotificationKind))]
    InvitationReceived = 30,

    /// <summary>Dispatched to one or more VIPs by the
    /// PR team via the "Notify VIPs" desk. Body is the rep's free-text
    /// message; sent as an in-app row + a queued email.</summary>
    [Display(Description = nameof(ResNotificationKind.VipBroadcast), ResourceType = typeof(ResNotificationKind))]
    VipBroadcast = 31,

    /// <summary>Dispatched to a visitor when their seat reservation for a
    /// session is confirmed (self-pick or random allocation). In-app row only
    /// — a low-stakes confirmation, no email.</summary>
    [Display(Description = nameof(ResNotificationKind.BookingConfirmed), ResourceType = typeof(ResNotificationKind))]
    BookingConfirmed = 40,

    /// <summary>Dispatched by the automated reminder worker to every
    /// attendee with an active seat in a session that is about to start.
    /// In-app row only.</summary>
    [Display(Description = nameof(ResNotificationKind.SessionReminder), ResourceType = typeof(ResNotificationKind))]
    SessionReminder = 41,

    /// <summary>Dispatched to a visitor when the
    /// Control Panel rejects their seat booking; the body carries the reason.
    /// In-app row only.</summary>
    [Display(Description = nameof(ResNotificationKind.BookingRejected), ResourceType = typeof(ResNotificationKind))]
    BookingRejected = 42,

    /// <summary>Dispatched to each participant when the
    /// Control Panel schedules an admin-arranged B2B/B2C business meeting for
    /// them. In-app row only.</summary>
    [Display(Description = nameof(ResNotificationKind.MeetingScheduled), ResourceType = typeof(ResNotificationKind))]
    MeetingScheduled = 43,

    /// <summary>Dispatched to each participant when the
    /// Control Panel cancels a confirmed business meeting. In-app row only.</summary>
    [Display(Description = nameof(ResNotificationKind.MeetingCancelled), ResourceType = typeof(ResNotificationKind))]
    MeetingCancelled = 44,

    /// <summary>Dispatched by the end-of-session rating-prompt worker to every
    /// attendee with an active seat in a session that has just ended, inviting
    /// them to rate it. <c>RelatedEntityType="Session"</c> + <c>RelatedEntityId</c>
    /// carry the session id so the app deep-links to its rating screen. In-app
    /// row only. Additive value (append-only — the frozen-enum rule).</summary>
    [Display(Description = nameof(ResNotificationKind.SessionRatingRequest), ResourceType = typeof(ResNotificationKind))]
    SessionRatingRequest = 45,

    /// <summary>Dispatched at the end of a programme day to every user who
    /// checked in that day (a GateScan). <c>RelatedEntityId</c> = the ProgrammeDay
    /// id; deep-links to <c>/rate?code=Day</c>. In-app row only. Additive value.</summary>
    [Display(Description = nameof(ResNotificationKind.DayRatingRequest), ResourceType = typeof(ResNotificationKind))]
    DayRatingRequest = 46,

    /// <summary>Dispatched after the last programme day to every user who
    /// checked in during the event, inviting an overall event rating
    /// (<c>/rate?code=Event</c>). In-app row only. Additive value.</summary>
    [Display(Description = nameof(ResNotificationKind.EventRatingRequest), ResourceType = typeof(ResNotificationKind))]
    EventRatingRequest = 47,

    /// <summary>Dispatched with the programme-end batch inviting an app
    /// rating (<c>/rate?code=App</c>). In-app row only. Additive value.</summary>
    [Display(Description = nameof(ResNotificationKind.AppRatingRequest), ResourceType = typeof(ResNotificationKind))]
    AppRatingRequest = 48,

    /// <summary>Dispatched with the programme-end batch inviting an
    /// exhibition rating (<c>/rate?code=Exhibition</c>). In-app row only.
    /// Additive value.</summary>
    [Display(Description = nameof(ResNotificationKind.ExhibitionRatingRequest), ResourceType = typeof(ResNotificationKind))]
    ExhibitionRatingRequest = 49,

    /// <summary>The requester's meeting is
    /// CONFIRMED because the speaker approved it via the double-opt-in email link.
    /// Distinct from the admin-arranged <see cref="MeetingScheduled"/>. In-app row
    /// only. Additive value (append-only, the frozen-enum rule).</summary>
    [Display(Description = nameof(ResNotificationKind.MeetingRequestConfirmed), ResourceType = typeof(ResNotificationKind))]
    MeetingRequestConfirmed = 50,

    /// <summary>Dispatched to an attendee when an administrator releases
    /// their held or confirmed seat reservation from the Control Panel (distinct
    /// from <see cref="BookingRejected"/>, a Pending booking declined with a
    /// reason). In-app row only. Additive value (append-only — the frozen-enum
    /// rule).</summary>
    [Display(Description = nameof(ResNotificationKind.BookingReleased), ResourceType = typeof(ResNotificationKind))]
    BookingReleased = 51,

    /// <summary>Dispatched to the requester when the Control Panel Accepts or
    /// Rejects their participation-document request. In-app row only. Additive value
    /// (append-only, the frozen-enum rule); persisted by NAME so no schema/data change.</summary>
    [Display(Description = nameof(ResNotificationKind.ParticipationDocumentDecided), ResourceType = typeof(ResNotificationKind))]
    ParticipationDocumentDecided = 52,

    /// <summary>Dispatched to the requester when the Control Panel Accepts or
    /// Rejects their badge-update request. In-app row only. Additive value.</summary>
    [Display(Description = nameof(ResNotificationKind.BadgeUpdateDecided), ResourceType = typeof(ResNotificationKind))]
    BadgeUpdateDecided = 53,

    /// <summary>Dispatched to the OTHER PARTY of a bilateral meeting
    /// (each eligible target-delegation member) when an admin approves a request and it
    /// now awaits their confirmation. Carries a confirm deep-link so the app can confirm
    /// on tap. <c>RelatedEntityType="DelegationMeetingRequest"</c> + <c>RelatedEntityId</c>.
    /// Sent as an in-app row + a queued email. Additive value (append-only, the
    /// frozen-enum rule); persisted by NAME so no schema/data change.</summary>
    [Display(Description = nameof(ResNotificationKind.MeetingRequested), ResourceType = typeof(ResNotificationKind))]
    MeetingRequested = 54,

    /// <summary>Dispatched by the automated reminder worker to both
    /// parties of a confirmed meeting about 15 minutes before it starts. Sent as an in-app
    /// row + a queued email (email is the real-time channel). Additive value (append-only,
    /// the frozen-enum rule); persisted by NAME so no schema/data change.</summary>
    [Display(Description = nameof(ResNotificationKind.MeetingReminder), ResourceType = typeof(ResNotificationKind))]
    MeetingReminder = 55,

    /// <summary>Manual admin broadcast from the Control Panel "Announcements" desk —
    /// a free-text bilingual message sent to a specific session's registered
    /// attendees or to a broad audience (e.g. session cancelled / hall changed /
    /// rescheduled). Sent as an in-app row + a queued email. When session-scoped,
    /// <c>RelatedEntityType="Session"</c> + <c>RelatedEntityId</c> carry the session
    /// id. Additive value (append-only, the frozen-enum rule); persisted by NAME so
    /// no schema/data change.</summary>
    [Display(Description = nameof(ResNotificationKind.AdminAnnouncement), ResourceType = typeof(ResNotificationKind))]
    AdminAnnouncement = 56,

    /// <summary>Dispatched to a visitor when an exhibitor captures
    /// them as a lead by scanning their entry badge, naming the exhibitor so the
    /// visitor knows who now holds their contact card. Raised once per NEW
    /// capture; an idempotent re-scan stays silent. In-app row only. Additive
    /// value (append-only, the frozen-enum rule); persisted by NAME so no
    /// schema/data change.</summary>
    [Display(Description = nameof(ResNotificationKind.ExhibitorLeadCaptured), ResourceType = typeof(ResNotificationKind))]
    ExhibitorLeadCaptured = 57,

    /// <summary>Dispatched when an administrator cancels (deactivates) a
    /// programme session. Before this the session simply vanished from the app's
    /// "my sessions" list and the public agenda with no message at all. Goes to
    /// everyone still holding an active seat for it AND everyone who favourited
    /// it (the two audiences whose agenda silently loses the card). Sent as an
    /// in-app row + a queued email. <c>RelatedEntityType="Session"</c> +
    /// <c>RelatedEntityId</c> carry the session id. Additive value (append-only,
    /// the frozen-enum rule); persisted by NAME so no schema/data change.</summary>
    [Display(Description = nameof(ResNotificationKind.SessionCancelled), ResourceType = typeof(ResNotificationKind))]
    // MERGE NOTE: authored as 57 on fix/qa-session-lifecycle-r2, renumbered to 58
    // at integration time because fix/qa-exhibitor-scan-authorization independently
    // took 57 for ExhibitorLeadCaptured. Both are additive and persisted by NAME,
    // so the renumber is safe and no data or wire contract is affected.
    SessionCancelled = 58,

    // The two kinds below name their resource KEY as a literal rather than through
    // nameof(ResNotificationKind.X): the accessor property and the two resx entries
    // land with the worker that dispatches each kind, and until they do a nameof
    // would not compile. GetDisplayName falls back to the enum name meanwhile, and
    // nothing in the product renders a NotificationKind label today (the notification
    // row carries its own bilingual title and body).

    /// <summary>Dispatched a short while after a session starts to every
    /// holder of an active seat reservation who has no <c>HallAttendance</c> row for
    /// it yet ("the session started and you have not arrived"). <c>RelatedEntityType
    /// ="Session"</c> + <c>RelatedEntityId</c> carry the session id. In-app row only.
    /// Additive value (append-only, the frozen-enum rule); persisted by NAME so no
    /// schema/data change.</summary>
    [Display(Description = "SessionNotAttended", ResourceType = typeof(ResNotificationKind))]
    SessionNotAttended = 59,

    /// <summary>Dispatched when the recommendation engine scores another
    /// attendee at or above the 80% match threshold, inviting the recipient to
    /// connect. Raised once per (caller, candidate) pair. <c>RelatedEntityType
    /// ="UserProfile"</c> + <c>RelatedEntityId</c> carry the candidate. In-app row
    /// only. Additive value (append-only, the frozen-enum rule); persisted by NAME
    /// so no schema/data change.</summary>
    [Display(Description = "MatchRecommended", ResourceType = typeof(ResNotificationKind))]
    MatchRecommended = 60,
}
