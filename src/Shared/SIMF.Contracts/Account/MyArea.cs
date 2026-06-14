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
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
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
    DateTimeOffset StartUtc,
    DateTimeOffset? EndUtc,
    string Summary,
    string? Location);

/// <summary>
/// The vCard data for the <c>contact-card.vcf</c> export (Page_014 E3). The
/// <see cref="QrId"/> is the badge's unique key (the same value the QR encodes).
/// </summary>
public sealed record MyAreaContactCard(
    string FullNameEn,
    string FullNameAr,
    string? JobTitle,
    string? Organisation,
    string? QrId);
