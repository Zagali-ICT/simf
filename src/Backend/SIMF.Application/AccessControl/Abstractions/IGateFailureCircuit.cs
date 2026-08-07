namespace SIMF.Application.AccessControl.Abstractions;

/// <summary>Per-gate failure-rate circuit
/// (SIMF-API-GATES-001 §10, SIMF-FDS-003 §5.6.6). ≥ 10 denials within a
/// rolling 60-second window for the same gate → 5-minute lockout. The
/// circuit emits one <c>GateFailureCircuitOpened</c> OperationLog row on
/// open and one <c>GateFailureCircuitClosed</c> on close so SOC can
/// correlate.</summary>
public interface IGateFailureCircuit
{
    /// <summary>Returns <c>true</c> when the circuit is open for this gate
    /// and the next scan should be rejected with 429
    /// GATE_FAILURE_CIRCUIT_OPEN.</summary>
    bool IsOpen(Guid gateId);

    /// <summary>Tell the circuit that a denial just happened. May transition
    /// the circuit to Open and write an OperationLog row.</summary>
    Task RecordDenialAsync(Guid gateId, CancellationToken cancellationToken = default);

    /// <summary>Tell the circuit that an allowed scan just happened — resets
    /// the consecutive-denial counter (the rolling window still drains
    /// independently).</summary>
    void RecordAllowed(Guid gateId);
}
