using System.Collections.Concurrent;
using SIMF.Application.Operations;
using SIMF.Contracts.Ops;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// In-process, thread-safe implementation of <see cref="IWorkerHeartbeatRegistry"/>.
/// A singleton shared by the API host and every hosted background worker: each
/// worker declares itself once at startup and then reports the outcome of each
/// tick, and <see cref="Snapshot"/> derives a live state per worker from the last
/// heartbeat relative to the worker's expected interval.
/// </summary>
internal sealed class WorkerHeartbeatRegistry(TimeProvider timeProvider) : IWorkerHeartbeatRegistry
{
    /// <summary>Extra tolerance added on top of the expected interval before a
    /// periodic worker that has stopped ticking is reported Stale.</summary>
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, Beat> _beats = new(StringComparer.Ordinal);

    public void Register(string workerName, string description, TimeSpan expectedInterval) =>
        _beats.AddOrUpdate(
            workerName,
            _ => new Beat(description, expectedInterval, timeProvider.GetUtcNow()),
            (_, existing) => existing with
            {
                Description = description,
                ExpectedInterval = expectedInterval,
            });

    public void RecordSuccess(string workerName)
    {
        var now = timeProvider.GetUtcNow();
        _beats.AddOrUpdate(
            workerName,
            _ => new Beat(workerName, TimeSpan.Zero, now)
            {
                LastRun = now,
                LastSuccess = now,
                RunCount = 1,
            },
            (_, e) => e with
            {
                LastRun = now,
                LastSuccess = now,
                // A successful tick clears any prior fault (contract: LastError is
                // null once a later success clears it).
                LastError = null,
                LastFailure = null,
                RunCount = e.RunCount + 1,
            });
    }

    public void RecordFailure(string workerName, string error)
    {
        var now = timeProvider.GetUtcNow();
        _beats.AddOrUpdate(
            workerName,
            _ => new Beat(workerName, TimeSpan.Zero, now)
            {
                LastRun = now,
                LastFailure = now,
                LastError = error,
                RunCount = 1,
                FailureCount = 1,
            },
            (_, e) => e with
            {
                LastRun = now,
                LastFailure = now,
                LastError = error,
                RunCount = e.RunCount + 1,
                FailureCount = e.FailureCount + 1,
            });
    }

    public WorkerStatusListResponse Snapshot()
    {
        var now = timeProvider.GetUtcNow();
        var workers = _beats
            .Select(kvp => ToStatus(kvp.Key, kvp.Value, now))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToList();

        return new WorkerStatusListResponse
        {
            Workers = workers,
            Generated = now,
            HealthyCount = workers.Count(w => w.State is WorkerState.Healthy or WorkerState.Starting),
            StaleCount = workers.Count(w => w.State == WorkerState.Stale),
            FaultedCount = workers.Count(w => w.State == WorkerState.Faulted),
        };
    }

    private static WorkerStatus ToStatus(string name, Beat b, DateTimeOffset now) =>
        new()
        {
            Name = name,
            Description = b.Description,
            State = DeriveState(b, now),
            LastRun = b.LastRun,
            LastSuccess = b.LastSuccess,
            LastFailure = b.LastFailure,
            LastError = b.LastError,
            RunCount = b.RunCount,
            FailureCount = b.FailureCount,
            ExpectedIntervalSeconds = (int)Math.Round(b.ExpectedInterval.TotalSeconds),
        };

    private static WorkerState DeriveState(Beat b, DateTimeOffset now)
    {
        // Most recent outcome was a failure: surface it until a later success clears it.
        if (b.LastFailure is { } failedAt
            && (b.LastSuccess is null || failedAt > b.LastSuccess))
        {
            return WorkerState.Faulted;
        }

        // Event-driven worker (no fixed interval, e.g. the email queue drain): up as
        // long as it is registered and its last outcome was not a failure.
        if (b.ExpectedInterval <= TimeSpan.Zero)
        {
            return WorkerState.Healthy;
        }

        var staleAfter = b.ExpectedInterval + b.ExpectedInterval + Grace;

        if (b.LastSuccess is { } okAt)
        {
            return now - okAt <= staleAfter ? WorkerState.Healthy : WorkerState.Stale;
        }

        // Never completed a tick yet: Starting through its first expected cycle,
        // then Stale if it still has not produced a success.
        return now - b.RegisteredUtc <= b.ExpectedInterval + staleAfter
            ? WorkerState.Starting
            : WorkerState.Stale;
    }

    private sealed record Beat(string Description, TimeSpan ExpectedInterval, DateTimeOffset RegisteredUtc)
    {
        public DateTimeOffset? LastRun { get; init; }
        public DateTimeOffset? LastSuccess { get; init; }
        public DateTimeOffset? LastFailure { get; init; }
        public string? LastError { get; init; }
        public long RunCount { get; init; }
        public long FailureCount { get; init; }
    }
}
