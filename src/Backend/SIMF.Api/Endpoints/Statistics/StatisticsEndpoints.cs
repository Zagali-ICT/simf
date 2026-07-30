// Tests: SIMF.Api.Tests/StatisticsTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Statistics.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Statistics;

namespace SIMF.Api.Endpoints.Statistics;

/// <summary>Vertical S — the Control Panel overview dashboard: live event
/// counts aggregated across both DbContexts. Administrator-only,
/// approved-account-only; read-only (no schema, no writes).</summary>
public sealed class GetStatisticsDashboardEndpoint(IStatisticsService service)
    : EndpointWithoutRequest<ApiResult<StatisticsDashboard>>
{
    public override void Configure()
    {
        Get("/admin/statistics");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Statistics.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<StatisticsDashboard>.Ok(
            await service.GetDashboardAsync(ct)), ct);
}

/// <summary>The programme dashboard: headline participant counts plus the
/// per-forum-day figures behind the Control Panel's 3-day chart. Same gate as
/// the overview dashboard (<c>Statistics.View</c> + approved account);
/// read-only, no schema, no writes.</summary>
public sealed class GetStatisticsProgrammeEndpoint(IStatisticsService service)
    : EndpointWithoutRequest<ApiResult<StatisticsProgramme>>
{
    public override void Configure()
    {
        Get("/admin/statistics/programme");
        Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Statistics.View),
                 nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        await Send.OkAsync(ApiResult<StatisticsProgramme>.Ok(
            await service.GetProgrammeAsync(ct)), ct);
}
