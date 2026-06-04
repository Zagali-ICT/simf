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
    int CommentsApproved,
    int CommentsPending,
    int RatingsCount,
    double AverageRating);
