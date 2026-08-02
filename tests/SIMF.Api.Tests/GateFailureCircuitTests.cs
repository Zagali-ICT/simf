// Unit tests for GateFailureCircuit (D-148, SIMF-FDS-003 §5.6.6,
// SIMF-API-GATES-001 §10). Rolling 60s window; ≥ 10 denials → open for
// 5 min; an allowed scan immediately clears the per-gate state.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Common.Enums;
using SIMF.Infrastructure.AccessControl;
using Xunit;
using SIMF.Common;

namespace SIMF.Api.Tests;

public sealed class GateFailureCircuitTests
{
    [Fact]
    public async Task Circuit_opens_after_ten_denials_in_window()
    {
        var time = new FakeTimeProvider(SimfClock.Now);
        var scopeFactory = new NoOpScopeFactory();
        var circuit = new GateFailureCircuit(scopeFactory, time, NullLogger<GateFailureCircuit>.Instance);
        var gateId = Guid.NewGuid();

        for (var i = 0; i < 9; i++)
        {
            await circuit.RecordDenialAsync(gateId);
        }
        Assert.False(circuit.IsOpen(gateId));

        await circuit.RecordDenialAsync(gateId); // tenth → opens
        Assert.True(circuit.IsOpen(gateId));
    }

    [Fact]
    public async Task Circuit_stays_closed_when_denials_drain_out_of_window()
    {
        var time = new FakeTimeProvider(SimfClock.Now);
        var circuit = new GateFailureCircuit(new NoOpScopeFactory(), time,
            NullLogger<GateFailureCircuit>.Instance);
        var gateId = Guid.NewGuid();

        for (var i = 0; i < 9; i++)
        {
            await circuit.RecordDenialAsync(gateId);
        }

        // Advance past the 60-second rolling window so the first nine denials
        // age out before the next one lands.
        time.Advance(TimeSpan.FromSeconds(61));

        await circuit.RecordDenialAsync(gateId);
        Assert.False(circuit.IsOpen(gateId));
    }

    [Fact]
    public async Task Allowed_scan_clears_the_window_and_closes_an_open_circuit()
    {
        var time = new FakeTimeProvider(SimfClock.Now);
        var circuit = new GateFailureCircuit(new NoOpScopeFactory(), time,
            NullLogger<GateFailureCircuit>.Instance);
        var gateId = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
        {
            await circuit.RecordDenialAsync(gateId);
        }
        Assert.True(circuit.IsOpen(gateId));

        circuit.RecordAllowed(gateId);
        Assert.False(circuit.IsOpen(gateId));
    }

    [Fact]
    public async Task Circuit_state_is_per_gate()
    {
        var time = new FakeTimeProvider(SimfClock.Now);
        var circuit = new GateFailureCircuit(new NoOpScopeFactory(), time,
            NullLogger<GateFailureCircuit>.Instance);
        var gateA = Guid.NewGuid();
        var gateB = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
        {
            await circuit.RecordDenialAsync(gateA);
        }
        Assert.True(circuit.IsOpen(gateA));
        Assert.False(circuit.IsOpen(gateB));
    }

    // The audit log writes are best-effort and the circuit resolves IAuditLog
    // from a scope, so a no-op scope factory backed by a no-op audit log is
    // enough for these unit tests.
    private sealed class NoOpScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new NoOpScope();
    }

    private sealed class NoOpScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new NoOpProvider();
        public void Dispose() { }
    }

    private sealed class NoOpProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IAuditLog) ? (object)new NoOpAuditLog() : null;
    }

    private sealed class NoOpAuditLog : IAuditLog
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
