using SIMF.Common.Enums;

namespace SIMF.Contracts.Requests;

/// <summary>D-500 (Wave 5, الطلبات 1408:9726) — which kind of request a unified
/// "My requests" row is. Display discriminator only (not persisted as such —
/// each kind has its own table); safe to extend. The app renders the type
/// headline (طلب لقاء مع متحدث / طلب حضور جلسة VIP / …) from this value.</summary>
public enum AppRequestKind
{
    /// <summary>A meeting the user requested with a speaker (D-269/D-475).</summary>
    SpeakerMeeting = 0,

    /// <summary>A delegation↔delegation meeting the user requested (D-478).
    /// Read-only in the app — managed on the Control Panel.</summary>
    DelegationMeeting = 1,

    /// <summary>A seat booking for a session (D-175/D-227) — the "طلب حضور جلسة
    /// VIP" row. Surfaced from the user's own seat reservations; no new entity
    /// (owner decision, D-500). Cancelled/managed from the join-session flow.</summary>
    SessionAttendance = 2,

    /// <summary>A participation-document request (D-500, طلب وثيقة المشاركة).</summary>
    ParticipationDocument = 3,

    /// <summary>A badge-update request (D-500, طلب تحديث البادج).</summary>
    BadgeUpdate = 4,
}

/// <summary>D-500 (Wave 5, الطلبات 1408:9726) — one row on the mobile unified
/// "My requests" screen: a request the signed-in user submitted, with its
/// current status. <see cref="Status"/> is the unified display state — seat
/// bookings map their <c>BookingStatus</c> onto this enum
/// (Approved→Accepted, released→Cancelled).</summary>
public sealed record AppRequestItem(
    AppRequestKind Kind,
    Guid Id,
    /// <summary>The context line under the type headline — the speaker name, the
    /// target country, the session·hall, the document label, or the requested
    /// job title (English; the app picks AR/EN by locale).</summary>
    string Title,
    string TitleArabic,
    MeetingRequestStatus Status,
    /// <summary>The date shown on the card — the session start / meeting slot;
    /// null falls back to <see cref="CreatedAt"/>.</summary>
    DateTimeOffset? EventDateUtc,
    DateTimeOffset CreatedAt,
    /// <summary>True when the signed-in user may cancel this request from the app
    /// (their own, still Pending, and a cancellable kind — speaker / document /
    /// badge; never delegation or session-attendance, which cancel elsewhere).</summary>
    bool CanCancel);

/// <summary>D-500 — body for <c>POST /app/my-requests/cancel</c>: the requester
/// withdraws one of their own still-pending requests.</summary>
public sealed class CancelMyRequestBody
{
    public AppRequestKind Kind { get; set; }
    public Guid Id { get; set; }
}
