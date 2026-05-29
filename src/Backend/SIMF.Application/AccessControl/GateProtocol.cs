namespace SIMF.Application.AccessControl;

/// <summary>Wire-level constants for the Gate Module HTTP protocol
/// (SIMF-API-GATES-001 §5 + §10).</summary>
public static class GateProtocol
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string IdempotentReplayHeader = "X-Idempotent-Replay";
    public const string FailureCircuitHeader = "X-Gate-Failure-Circuit";
    public const string FailureCircuitOpenValue = "open";
}
