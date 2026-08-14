// Tests: SIMF.Api.Tests/NotificationKindCatalogTests.cs
using SIMF.Common.Enums;

namespace SIMF.Application.Notifications;

/// <summary>The single source of truth mapping each
/// <see cref="NotificationKind"/> to its default group code + app-internal
/// deep-link. <see cref="NotificationDispatcher"/> stamps every row from here
/// when the dispatch request leaves the values null, so all ~16 existing call
/// sites are untouched and a new kind adds exactly one arm below. A dispatch
/// request may still override either value.</summary>
public static class NotificationKindCatalog
{
    /// <summary>The stable group codes the app sections the notification list
    /// by (they mirror the app's filter chips).</summary>
    public static class Groups
    {
        public const string Account = "Account";
        public const string Vip = "Vip";
        public const string Bookings = "Bookings";
        public const string Sessions = "Sessions";
        public const string Meetings = "Meetings";
        public const string Ratings = "Ratings";
    }

    /// <summary>The group code for <paramref name="kind"/>; null when the kind
    /// belongs to no group (it then falls into the "all" bucket only).</summary>
    public static string? GroupFor(NotificationKind kind) => kind switch
    {
        NotificationKind.CredentialEmailVerificationSent or
        NotificationKind.CredentialEmailVerificationResent or
        NotificationKind.CredentialSignInOtpSent or
        NotificationKind.CredentialPasswordResetRequested or
        NotificationKind.AccountProfileSubmitted or
        NotificationKind.AdminPendingVisitor or
        NotificationKind.AccountApproved or
        NotificationKind.AccountRejected or
        NotificationKind.AccountTwoFactorReset or
        NotificationKind.AccountWelcome or
        NotificationKind.AccountPasswordChanged or
        NotificationKind.AccountPasswordResetCompleted or
        NotificationKind.AdminPendingApproval or
        // R-2 — document/badge request outcomes are personal "My Requests" results,
        // not event-flow items; they belong with the account section (there is no
        // separate Requests chip), matching the app's default grouping.
        NotificationKind.ParticipationDocumentDecided or
        NotificationKind.BadgeUpdateDecided or
        // Manual admin broadcast — the default group for an audience-wide announcement.
        // A session-scoped broadcast overrides Group to Sessions at dispatch time.
        NotificationKind.AdminAnnouncement or
        // DEF-EXH-002 — "an exhibitor now holds your contact card" is a personal
        // privacy notice about the holder's own data, so it sits with the account
        // section (there is no separate privacy chip).
        NotificationKind.ExhibitorLeadCaptured or
        // A credential was bound to the account, which is the same kind of
        // security notice as a password change, so it groups with them.
        NotificationKind.DeviceKeyEnrolled => Groups.Account,

        NotificationKind.InvitationReceived or
        NotificationKind.VipBroadcast => Groups.Vip,

        NotificationKind.BookingConfirmed or
        NotificationKind.BookingRejected or
        // M-4 — an admin-released seat is part of the booking lifecycle.
        NotificationKind.BookingReleased => Groups.Bookings,

        NotificationKind.SessionReminder or
        // B2 — an admin-cancelled session is a programme event, not a booking
        // outcome: it belongs with the app's Sessions filter chip.
        NotificationKind.SessionCancelled or
        // FR-903 (C4) — "the session started and you have not arrived" is about a
        // session the holder already booked, so it reads under Sessions rather than
        // Bookings: the booking itself is still valid, only the attendance is missing.
        NotificationKind.SessionNotAttended => Groups.Sessions,

        NotificationKind.MeetingScheduled or
        NotificationKind.MeetingCancelled or
        NotificationKind.MeetingRequestConfirmed or
        // Bi-Meeting rework — the other-party request-to-confirm + the 15-min reminder
        // both belong with the app's Meetings filter chip.
        NotificationKind.MeetingRequested or
        NotificationKind.MeetingReminder or
        // FR-803 (C5) — a scored match is an invitation to meet someone, so it
        // belongs with the app's Meetings filter chip alongside the rest of the
        // bilateral-meeting lifecycle.
        NotificationKind.MatchRecommended => Groups.Meetings,

        NotificationKind.SessionRatingRequest or
        NotificationKind.DayRatingRequest or
        NotificationKind.EventRatingRequest or
        NotificationKind.AppRatingRequest or
        NotificationKind.ExhibitionRatingRequest => Groups.Ratings,

        _ => null,
    };

    /// <summary>The app-internal deep-link for <paramref name="kind"/>; null when
    /// the tile is informational (no navigation). <paramref name="relatedId"/>
    /// fills the target for the per-target kinds — a per-target kind with no id
    /// yields null rather than a broken link.</summary>
    public static string? ClickUrlFor(NotificationKind kind, Guid? relatedId) => kind switch
    {
        NotificationKind.BookingConfirmed => "/badge",
        // Bi-Meeting rework — the other party confirms the meeting on tap; the app opens the
        // meeting-confirm screen for this delegation request (route wired in the mobile phase).
        NotificationKind.MeetingRequested when relatedId is { } requestId =>
            $"/meeting-confirm?requestId={requestId}",
        // QA A27 — every meeting-lifecycle tile is navigable: scheduled / cancelled /
        // confirmed / the 15-minute reminder all open the bilateral-meetings page, where the
        // requester's speaker AND delegation meetings live. These four kinds are not
        // per-target (the app has no meeting-detail route), so they carry no id.
        NotificationKind.MeetingScheduled or
        NotificationKind.MeetingCancelled or
        NotificationKind.MeetingRequestConfirmed or
        NotificationKind.MeetingReminder => "/meetings",
        // FR-803 (C5) — the recommended candidate is the networking surface's
        // subject, so the tile opens المقابلات/networking rather than the
        // bilateral-meetings list: no meeting exists yet, only a suggestion.
        NotificationKind.MatchRecommended => "/meet",
        NotificationKind.SessionRatingRequest when relatedId is { } sessionId =>
            $"/rate?code=Session&targetId={sessionId}",
        NotificationKind.DayRatingRequest when relatedId is { } dayId =>
            $"/rate?code=Day&targetId={dayId}",
        NotificationKind.EventRatingRequest => "/rate?code=Event",
        NotificationKind.AppRatingRequest => "/rate?code=App",
        NotificationKind.ExhibitionRatingRequest => "/rate?code=Exhibition",
        // B2 — deliberately NO deep link: the cancelled session is soft-deleted, so
        // its detail screen would 404. The tile is informational only.
        NotificationKind.SessionCancelled => null,
        _ => null,
    };
}
