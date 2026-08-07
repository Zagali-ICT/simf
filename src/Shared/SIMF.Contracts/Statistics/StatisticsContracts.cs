namespace SIMF.Contracts.Statistics;

/// <summary>
/// Vertical S — a flat snapshot of live event counts for the Control Panel
/// overview dashboard. Served by <c>GET /api/v1/admin/statistics</c>. Every
/// field is an aggregate read computed on demand (no stored snapshot table);
/// each count is a single COUNT query against the live data so the dashboard
/// always reflects the current state.
///
/// <para>The fields mirror the countable surface present on the two
/// DbContexts: attendees / approvals come from the Identity DB (SimfUser),
/// the event-module counts come from the App DB. Companies are intentionally
/// omitted from this increment — they are owned by a separate vertical and
/// may not be present; the count can be folded in later without changing the
/// existing field shape (additive record extension).</para>
/// </summary>
public sealed record StatisticsDashboard(
    int TotalAttendees,
    int ApprovedAttendees,
    int PendingApprovals,
    int Sessions,
    int Speakers,
    int Booths,
    int Sponsors,
    int NewsArticles,
    int MediaItems,
    int RatingsCount,
    double AverageRating);

/// <summary>
/// One forum day of the Control Panel programme dashboard — the figures the
/// organisers watch per day, keyed to a <c>ProgrammeDay</c> row.
///
/// <para>Every count is bucketed on the <b>Saudi calendar day</b>. Instants are
/// persisted as the Saudi wall clock, so the stored value already
/// falls in the right day and no zone shift is involved; the service still
/// resolves each day to an explicit half-open window <c>[start, end)</c>,
/// because a range predicate stays an index-friendly seek where a date-component
/// expression would not.</para>
///
/// <para><see cref="Present"/> and <see cref="Attended"/> are <b>distinct</b>
/// person counts and use different identifiers (a gate scan resolves a
/// <c>UserProfile.Id</c>; a hall arrival records the Identity <c>UserId</c>), so
/// they are sibling figures — one is not a subset of the other.</para>
/// </summary>
public sealed record ProgrammeDayStats(
    Guid DayId,
    DateOnly Date,
    string Title,
    string TitleArabic,
    int DisplayOrder,
    int Registered,
    int Present,
    int Sessions,
    int Attended);

/// <summary>
/// The Control Panel programme dashboard: the headline participant counts plus
/// one <see cref="ProgrammeDayStats"/> per active forum day, in display order.
/// Served by <c>GET /api/v1/admin/statistics/programme</c>.
///
/// <para>Additive to <see cref="StatisticsDashboard"/> rather than folded into
/// it, so the existing dashboard contract and its consumers stay untouched.</para>
///
/// <para>Role counts are resolved through <c>UserProfile</c> →
/// <c>UserProfileType</c>, which both live in the App database — the mapping is
/// admin-curated data (<c>ProfileType.MobileAppRole</c> / <c>IsForVisitor</c>),
/// never hardcoded, and never a cross-database join.</para>
/// </summary>
public sealed record StatisticsProgramme(
    int CurrentUsers,
    int Visitors,
    int Staff,
    int Moderators,
    int Exhibitors,
    int ExhibitorAccounts,
    int Sponsors,
    int Speakers,
    int Booths,
    int TotalAttended,
    IReadOnlyList<ProgrammeDayStats> Days);
