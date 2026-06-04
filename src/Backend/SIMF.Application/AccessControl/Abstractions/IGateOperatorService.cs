using SIMF.Contracts.Gates;

namespace SIMF.Application.AccessControl.Abstractions;

/// <summary>D-148 — the operator surface (SIMF-API-GATES-001 §7). Hosts
/// the 13-step constraint engine for <c>POST /scans</c>.</summary>
public interface IGateOperatorService
{
    Task<IReadOnlyList<OperatorGateAssignment>> ListMyAssignmentsAsync(
        Guid operatorUserId, CancellationToken cancellationToken = default);

    Task<GateScanResult> RecordScanAsync(
        GateScanContext context, CancellationToken cancellationToken = default);

    Task<OperatorDailyReport> GetMyDailyReportAsync(
        Guid operatorUserId, Guid? gateId,
        CancellationToken cancellationToken = default);

    /// <summary>D-160 — cursor-paged list of scans at a single gate, for
    /// the staff app's "who's at this gate" view (SIMF-API-GATES-001 §7.4).
    /// Returns null when the operator is not assigned to the gate
    /// (handled as 403 at the endpoint).</summary>
    Task<GateVisitorsListResult> ListGateVisitorsAsync(
        Guid operatorUserId, Guid gateId, GateVisitorsListRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>D-160 — result envelope for
/// <see cref="IGateOperatorService.ListGateVisitorsAsync"/>.</summary>
public sealed record GateVisitorsListResult(
    GateVisitorsListResultKind Kind,
    GateVisitorsListResponse? Response);

public enum GateVisitorsListResultKind
{
    Ok = 0,
    GateNotFound = 1,
    NotAssigned = 2,
}

/// <summary>D-148 — the per-request envelope the engine consumes. Carries
/// the operator identity + the scan parameters + the correlation context
/// the audit row needs.</summary>
public sealed class GateScanContext
{
    public required Guid GateId { get; init; }
    public required Guid OperatorUserId { get; init; }
    public required GateScanRequest Request { get; init; }
    public string? CorrelationId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? AcceptLanguage { get; init; }
    /// <summary>Idempotency key from the request header — header wins over
    /// body when both are present (SIMF-API-GATES-001 §9).</summary>
    public string? HeaderIdempotencyKey { get; init; }
}

/// <summary>D-148 — the engine's result envelope.</summary>
public sealed record GateScanResult(
    GateScanResultKind Kind,
    GateScanResponse Response,
    bool IsIdempotentReplay,
    string? FailureReasonForHttp);

public enum GateScanResultKind
{
    /// <summary>Recorded scan (Allowed or Denied) — HTTP 200.</summary>
    Recorded = 0,
    /// <summary>HTTP 404 GATE_NOT_FOUND.</summary>
    GateNotFound = 1,
    /// <summary>HTTP 403 GATE_OPERATOR_NOT_ASSIGNED.</summary>
    NotAssigned = 2,
    /// <summary>HTTP 503 GATE_INACTIVE (pre-engine).</summary>
    GateInactive = 3,
    /// <summary>HTTP 409 IDEMPOTENCY_KEY_CONFLICT.</summary>
    IdempotencyConflict = 4,
    /// <summary>HTTP 429 GATE_FAILURE_CIRCUIT_OPEN.</summary>
    CircuitOpen = 5,
}
