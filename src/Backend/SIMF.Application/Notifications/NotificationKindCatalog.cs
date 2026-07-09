// Tests: SIMF.Api.Tests/NotificationKindCatalogTests.cs
using SIMF.Common.Enums;

namespace SIMF.Application.Notifications;

/// <summary>D-677 — the single source of truth mapping each
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
        NotificationKind.AdminPendingApproval => Groups.Account,

        NotificationKind.InvitationReceived or
        NotificationKind.VipBroadcast => Groups.Vip,

        NotificationKind.BookingConfirmed or
        NotificationKind.BookingRejected => Groups.Bookings,

        NotificationKind.SessionReminder => Groups.Sessions,

        NotificationKind.MeetingScheduled or
        NotificationKind.MeetingCancelled or
        NotificationKind.MeetingRequestConfirmed => Groups.Meetings,

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
        NotificationKind.SessionRatingRequest when relatedId is { } sessionId =>
            $"/rate?code=Session&targetId={sessionId}",
        NotificationKind.DayRatingRequest when relatedId is { } dayId =>
            $"/rate?code=Day&targetId={dayId}",
        NotificationKind.EventRatingRequest => "/rate?code=Event",
        NotificationKind.AppRatingRequest => "/rate?code=App",
        NotificationKind.ExhibitionRatingRequest => "/rate?code=Exhibition",
        _ => null,
    };
}
