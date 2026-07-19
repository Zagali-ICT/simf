using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.Operations;
using SIMF.Common;
using SIMF.Contracts.Ops;

namespace SIMF.Api.Endpoints.Admin.Ops;

/// <summary>
/// <c>GET /api/v1/admin/ops/workers</c> - the live status of every in-process
/// background worker, read from the heartbeat registry, for the CP services
/// monitor. Gated by the ServicesMonitor.View permission.
/// </summary>
public sealed class WorkerStatusEndpoint(IWorkerHeartbeatRegistry registry)
    : EndpointWithoutRequest<ApiResult<WorkerStatusListResponse>>
{
    public override void Configure()
    {
        Get("/admin/ops/workers");
        Policies(
            PermissionCatalog.PolicyFor(PermissionCatalog.ServicesMonitor.View),
            nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Summary(summary => summary.Summary =
            "List background-worker health. Requires the ServicesMonitor.View permission.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(
            ApiResult<WorkerStatusListResponse>.Ok(registry.Snapshot()), ct);
    }
}
