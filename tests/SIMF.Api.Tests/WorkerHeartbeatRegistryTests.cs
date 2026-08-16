// Unit tests for the in-process worker heartbeat registry that backs the CP
// services monitor and the /health "workers" check. Covers the state machine:
// Starting -> Healthy -> Stale, Faulted-and-recover, the event-driven (no
// interval) worker, and the roll-up counts. Uses FakeTimeProvider so the
// staleness thresholds are exercised deterministically.
using Microsoft.Extensions.Time.Testing;
using SIMF.Contracts.Ops;
using SIMF.Infrastructure.Operations;
using Xunit;
using SIMF.Common;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class WorkerHeartbeatRegistryTests
{
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

    private static WorkerStatus Only(WorkerStatusListResponse snapshot) =>
        Assert.Single(snapshot.Workers);

    private static WorkerHeartbeatRegistry NewRegistry(out FakeTimeProvider time)
    {
        time = new FakeTimeProvider(SimfClock.Now);
        return new WorkerHeartbeatRegistry(time);
    }

    [Fact]
    public void Registered_worker_is_Starting_before_its_first_tick()
    {
        var registry = NewRegistry(out _);

        registry.Register("W", "desc", OneMinute);

        var worker = Only(registry.Snapshot());
        Assert.Equal("W", worker.Name);
        Assert.Equal("desc", worker.Description);
        Assert.Equal(WorkerState.Starting, worker.State);
        Assert.Equal(0L, worker.RunCount);
    }

    [Fact]
    public void Success_marks_Healthy_and_counts_the_run()
    {
        var registry = NewRegistry(out _);
        registry.Register("W", "desc", OneMinute);

        registry.RecordSuccess("W");

        var worker = Only(registry.Snapshot());
        Assert.Equal(WorkerState.Healthy, worker.State);
        Assert.Equal(1L, worker.RunCount);
        Assert.NotNull(worker.LastSuccess);
    }

    [Fact]
    public void Never_ticking_worker_becomes_Stale_past_its_first_window()
    {
        var registry = NewRegistry(out var time);
        registry.Register("W", "desc", OneMinute);

        // Starting window for a 1m worker is interval + (2*interval + 60s) = 4m.
        time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(WorkerState.Stale, Only(registry.Snapshot()).State);
    }

    [Fact]
    public void Healthy_worker_goes_Stale_when_it_stops_ticking()
    {
        var registry = NewRegistry(out var time);
        registry.Register("W", "desc", OneMinute);
        registry.RecordSuccess("W");
        Assert.Equal(WorkerState.Healthy, Only(registry.Snapshot()).State);

        // staleAfter for a 1m worker is 2*interval + 60s = 3m.
        time.Advance(TimeSpan.FromMinutes(4));

        Assert.Equal(WorkerState.Stale, Only(registry.Snapshot()).State);
    }

    [Fact]
    public void Failure_marks_Faulted_with_the_message_then_a_success_clears_it()
    {
        var registry = NewRegistry(out _);
        registry.Register("W", "desc", OneMinute);

        registry.RecordFailure("W", "boom");

        var faulted = Only(registry.Snapshot());
        Assert.Equal(WorkerState.Faulted, faulted.State);
        Assert.Equal("boom", faulted.LastError);
        Assert.Equal(1L, faulted.FailureCount);

        registry.RecordSuccess("W");

        var recovered = Only(registry.Snapshot());
        Assert.Equal(WorkerState.Healthy, recovered.State);
        // Recovery must clear the stale error text (contract), not just the state.
        Assert.Null(recovered.LastError);
    }

    [Fact]
    public void Event_driven_worker_stays_Healthy_however_long_it_is_idle()
    {
        var registry = NewRegistry(out var time);
        registry.Register("Email", "desc", TimeSpan.Zero);

        time.Advance(TimeSpan.FromHours(6));

        Assert.Equal(WorkerState.Healthy, Only(registry.Snapshot()).State);
    }

    [Fact]
    public void Snapshot_rolls_up_the_state_counts()
    {
        var registry = NewRegistry(out _);
        registry.Register("Ok", "d", OneMinute);
        registry.RecordSuccess("Ok");
        registry.Register("Bad", "d", OneMinute);
        registry.RecordFailure("Bad", "x");

        var snapshot = registry.Snapshot();

        Assert.Equal(2, snapshot.Workers.Count);
        Assert.Equal(1, snapshot.HealthyCount);
        Assert.Equal(1, snapshot.FaultedCount);
        Assert.Equal(0, snapshot.StaleCount);
    }

    [Fact]
    public void Register_is_idempotent_and_keeps_the_accumulated_counts()
    {
        var registry = NewRegistry(out _);
        registry.Register("W", "old", OneMinute);
        registry.RecordSuccess("W");

        // A second Register (e.g. a worker restart within the same process) must
        // refresh the description without resetting the run history.
        registry.Register("W", "new", OneMinute);

        var worker = Only(registry.Snapshot());
        Assert.Equal("new", worker.Description);
        Assert.Equal(1L, worker.RunCount);
    }
}
