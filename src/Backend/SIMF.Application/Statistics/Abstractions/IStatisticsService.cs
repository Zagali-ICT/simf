using SIMF.Contracts.Statistics;

namespace SIMF.Application.Statistics.Abstractions;

/// <summary>
/// Vertical S — builds the Control Panel overview dashboard: a flat set of
/// live event counts aggregated across the Identity DB (attendees / approvals)
/// and the App DB (sessions, speakers, booths, sponsors, news, media,
/// delegations, comments, ratings). Read-only — no schema, no writes.
/// </summary>
public interface IStatisticsService
{
    Task<StatisticsDashboard> GetDashboardAsync(CancellationToken cancellationToken = default);
}
