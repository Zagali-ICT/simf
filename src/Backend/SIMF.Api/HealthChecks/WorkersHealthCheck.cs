using Microsoft.Extensions.Diagnostics.HealthChecks;
using SIMF.Application.Operations;
using SIMF.Contracts.Ops;

namespace SIMF.Api.HealthChecks;

/// <summary>
/// Reports the background-worker tier's health from the in-process heartbeat
/// registry, surfaced on /health: Healthy when every worker is up, Degraded when
/// one has missed its schedule (Stale), Unhealthy when one has faulted. Each
/// worker's state is attached as check data for the readiness probe.
/// </summary>
internal sealed class WorkersHealthCheck(IWorkerHeartbeatRegistry registry) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = registry.Snapshot();

        var data = snapshot.Workers.ToDictionary(
            w => w.Name,
            w => (object)w.State.ToString());

        if (snapshot.FaultedCount > 0)
        {
            var faulted = string.Join(", ", snapshot.Workers
                .Where(w => w.State == WorkerState.Faulted).Select(w => w.Name));
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"{snapshot.FaultedCount} background worker(s) faulted: {faulted}", data: data));
        }

        if (snapshot.StaleCount > 0)
        {
            var stale = string.Join(", ", snapshot.Workers
                .Where(w => w.State == WorkerState.Stale).Select(w => w.Name));
            return Task.FromResult(HealthCheckResult.Degraded(
                $"{snapshot.StaleCount} background worker(s) stale: {stale}", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"{snapshot.Workers.Count} background worker(s) up", data: data));
    }
}
