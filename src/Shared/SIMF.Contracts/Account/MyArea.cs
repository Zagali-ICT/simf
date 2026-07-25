using SIMF.Common.Enums;

namespace SIMF.Contracts.Account;

/// <summary>
/// The My-Area (منطقتي) personal dashboard — App Screen 14 (Page_014). One
/// additive, read-only aggregate over existing App-DB tables: the identity
/// card, the two stat counters, and today's merged schedule. No schema change,
/// no migration (D-249). Counter / schedule rules live in
/// <c>docs/App/Page_014/Page_014_Logic.md</c>.
/// </summary>
public sealed record MyAreaDashboard(
    MyAreaIdentity Identity,
    MyAreaCounters Counters,
    IReadOnlyList<MyAreaScheduleItem> TodaySchedule);

/// <summary>
/// The identity card. Names + QR id come from the user's <c>UserProfile</c>;
/// the tier (name + colour) from the assigned <c>ProfileType</c>; the avatar
/// from the account. <see cref="QrId"/> is null until the account is Approved
/// (Page_014 L-1) — the card still renders, but the badge QR is hidden.
/// </summary>
public sealed record MyAreaIdentity(
    string FullNameAr,
    string FullNameEn,
    string? QrId,
    string? AvatarUrl,
    string? TierNameEn,
    string? TierNameAr,
    string? PageColor,
    // True for audience profile types, false for partner/exhibitor ("Other")
    // types — drives the QR-page actions (visitor: read/share contact;
    // exhibitor: scan visitor badges → My Visitors). Defaults true when no
    // ProfileType is assigned (D-426). Additive wire field.
    bool IsVisitor = true);

/// <summary>The two stat counters (Page_014 L-2, L-3).</summary>
public sealed record MyAreaCounters(
    int BookedSessionsCount,
    int MeetingsCount);

/// <summary>
/// One row in the merged, time-ordered schedule (Page_014 L-4).
/// <list type="bullet">
///   <item><see cref="Kind"/> = <c>"Session"</c> — a held seat booking; carries
///     its <see cref="SessionId"/>, <see cref="MeetingId"/> null, no subject.</item>
///   <item><see cref="Kind"/> = <c>"Meeting"</c> — either an accepted speaker
///     meeting (carries the parent <see cref="SessionId"/> + a session title) or
///     a confirmed business meeting (<see cref="MeetingId"/> only, empty title,
///     the subject carries the meeting note).</item>
/// </list>
/// Titles + hall names come paired (EN/AR); the app selects per locale.
/// </summary>
public sealed record MyAreaScheduleItem(
    string Kind,
    DateTimeOffset Start,
    DateTimeOffset? End,
    string TitleEn,
    string TitleAr,
    string? HallNameEn,
    string? HallNameAr,
    string? Subject,
    string Status,
    Guid? SessionId,
    Guid? MeetingId);

/// <summary>
/// One flat calendar event for the <c>calendar.ics</c> export (Page_014 E2) —
/// one per held session + accepted speaker meeting + confirmed business meeting,
/// across <b>all</b> days (not just today). The endpoint renders these as
/// RFC 5545 VEVENTs.
/// </summary>
public sealed record MyAreaCalendarEvent(
    Guid Uid,
    DateTimeOffset Start,
    DateTimeOffset? End,
    string Summary,
    string? Location);

/// <summary>
/// The "my sessions" list — App "تفاصيل الجلسات" (Figma 1388:9067), reached from
/// the My-Area "my sessions" counter. The user's booked / joined sessions (the
/// same active seat-bookings the dashboard counts), each enriched with the
/// per-user heart (<see cref="IsFavourite"/>) and whether the user actually
/// arrived (<see cref="Attended"/>, derived from <c>HallAttendance</c> — no
/// schema). The app partitions these into the four tabs client-side from the
/// device clock: القادمة (upcoming = <see cref="Start"/> in the future),
/// حضرتها (<see cref="Attended"/>), فاتتني (ended &amp; not attended), and الأرشيف
/// (recorded / published — <see cref="Status"/>). Read-only aggregate, own
/// <c>sub</c>; no migration (D-249 pattern). <see cref="SessionFavourite"/>
/// powers the heart.
/// </summary>
public sealed record MyAreaSessions(IReadOnlyList<MyAreaSessionItem> Items);

/// <summary>
/// One card on the "my sessions" list (Figma 1388:9067). The card shows the
/// title, the day/time line + the category chip, and the primary speaker
/// (name + <see cref="SpeakerTitle"/> rank) with the hall. Bilingual fields are
/// paired (the app picks per locale); the hall / category / speaker fields are
/// null when the session has none. <see cref="Attended"/> and
/// <see cref="IsFavourite"/> are the two per-user flags.
/// </summary>
public sealed record MyAreaSessionItem(
    Guid Id,
    string Title,
    string TitleArabic,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? HallNameEn,
    string? HallNameAr,
    string? CategoryNameEn,
    string? CategoryNameAr,
    string? SpeakerNameEn,
    string? SpeakerNameAr,
    string? SpeakerTitle,
    SessionStatus Status,
    bool Attended,
    bool IsFavourite);

/// <summary>
/// The vCard data for the <c>contact-card.vcf</c> export (Page_014 E3) and the
/// share-my-contact QR (D-470). <see cref="QrId"/> is the badge's gate key and
/// is deliberately NOT emitted into the camera-readable vCard. The mobile
/// numbers feed the vCard <c>TEL</c> lines (D-470, requirement #8 "Name ar,
/// phones").
/// </summary>
public sealed record MyAreaContactCard(
    string FullNameEn,
    string FullNameAr,
    string? JobTitle,
    // 2026-07-20 — Arabic twin of JobTitle for the bilingual vCard TITLE.
    string? JobTitleArabic,
    string? Organisation,
    string? QrId,
    string? SaudiMobile = null,
    string? InternationalMobile = null);
