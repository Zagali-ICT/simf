using SIMF.Contracts.Statistics;

namespace SIMF.Application.Statistics.Abstractions;

/// <summary>
/// Vertical S — builds the Control Panel overview dashboard: a flat set of
/// live event counts aggregated across the Identity DB (attendees / approvals)
/// and the App DB (sessions, speakers, booths, sponsors, news, media,
/// delegations, ratings). Read-only — no schema, no writes.
/// </summary>
public interface IStatisticsService
{
    Task<StatisticsDashboard> GetDashboardAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the programme dashboard: the headline participant counts plus the
    /// per-forum-day figures (registered / present / sessions / attended), one
    /// entry per active <c>ProgrammeDay</c> in display order. Read-only — no
    /// schema, no writes. Day buckets are Saudi calendar days.
    /// </summary>
    Task<StatisticsProgramme> GetProgrammeAsync(CancellationToken cancellationToken = default);
}
