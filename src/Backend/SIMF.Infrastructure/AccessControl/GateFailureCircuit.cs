// Tests: SIMF.Api.Tests/Gates/GateFailureCircuitTests.cs
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.AccessControl;

/// <summary>
/// D-148 — per-gate failure-rate circuit
/// (SIMF-API-GATES-001 §10, SIMF-FDS-003 §5.6.6). Single-process in-memory
/// state (one process per environment in this increment).
///
/// Rolling 60-second window. ≥ 10 denials in the window → open the circuit
/// for 5 minutes. Allowed scans clear the per-gate state immediately.
///
/// <para>G-3 — <see cref="RecordDenialAsync"/> must be called only for SYSTEM
/// faults (backend/infra failures), never for benign policy denials: the caller
/// (<c>GateOperatorService</c>) gates the call so normal traffic — unknown QRs,
/// unapproved holders — cannot trip a venue-wide 5-minute outage.</para>
/// </summary>
internal sealed class GateFailureCircuit(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<GateFailureCircuit> logger) : IGateFailureCircuit
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);
    private const int DenialThreshold = 10;

    private readonly ConcurrentDictionary<Guid, CircuitState> states = new();

    public bool IsOpen(Guid gateId)
    {
        if (!states.TryGetValue(gateId, out var state)) { return false; }
        lock (state)
        {
            if (state.OpenUntil is { } until && until > timeProvider.GetUtcNow())
            {
                return true;
            }
            return false;
        }
    }

    public Task RecordDenialAsync(Guid gateId, CancellationToken cancellationToken = default)
    {
        var state = states.GetOrAdd(gateId, _ => new CircuitState());
        bool justOpened = false;
        lock (state)
        {
            var now = timeProvider.GetUtcNow();
            state.RecentDenials.Enqueue(now);
            while (state.RecentDenials.TryPeek(out var oldest) && now - oldest > Window)
            {
                state.RecentDenials.Dequeue();
            }

            if (state.OpenUntil is { } until && until > now) { return Task.CompletedTask; }

            if (state.RecentDenials.Count >= DenialThreshold)
            {
                state.OpenUntil = now + LockoutDuration;
                justOpened = true;
            }
        }

        if (justOpened)
        {
            logger.LogWarning(
                "Gate failure-rate circuit OPENED for {GateId} ({Count} denials in {WindowSeconds}s)",
                gateId, DenialThreshold, Window.TotalSeconds);
            return WriteAuditAsync(AuditEvents.GateFailureCircuitOpened,
                AuditOutcome.Failure,
                $"gateId={gateId}; threshold={DenialThreshold}; window={Window.TotalSeconds}s; lockout={LockoutDuration.TotalMinutes}m",
                cancellationToken);
        }
        return Task.CompletedTask;
    }

    private async Task WriteAuditAsync(
        string eventType, AuditOutcome outcome, string detail,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = eventType,
            Outcome = outcome,
            Detail = detail,
        }, cancellationToken);
    }

    public void RecordAllowed(Guid gateId)
    {
        if (!states.TryGetValue(gateId, out var state)) { return; }
        bool wasOpen = false;
        lock (state)
        {
            if (state.OpenUntil is { } until && until > timeProvider.GetUtcNow())
            {
                wasOpen = true;
                state.OpenUntil = null;
            }
            state.RecentDenials.Clear();
        }
        if (wasOpen)
        {
            logger.LogInformation(
                "Gate failure-rate circuit CLOSED for {GateId} (allowed scan recorded)",
                gateId);
            _ = WriteAuditAsync(AuditEvents.GateFailureCircuitClosed,
                AuditOutcome.Success,
                $"gateId={gateId}; reason=allowed-scan",
                CancellationToken.None);
        }
    }

    private sealed class CircuitState
    {
        public Queue<DateTimeOffset> RecentDenials { get; } = new();
        public DateTimeOffset? OpenUntil { get; set; }
    }
}
