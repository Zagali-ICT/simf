using SIMF.Contracts.Gates;

namespace SIMF.Application.AccessControl.Abstractions;

/// <summary>The operator surface (SIMF-API-GATES-001 §7). Hosts
/// the 13-step constraint engine for <c>POST /scans</c>.</summary>
public interface IGateOperatorService
{
    Task<IReadOnlyList<OperatorGateAssignment>> ListMyAssignmentsAsync(
        Guid operatorUserId, CancellationToken cancellationToken = default);

    /// <summary>The snapshot a scanner caches so it can judge a badge
    /// with no network: this operator's gates, each with the profile-type CODES
    /// it admits, plus the badge key when offline badges are armed. Kept off
    /// <see cref="ListMyAssignmentsAsync"/> deliberately — it carries a secret,
    /// so it gets its own endpoint that can be watched and disabled on its
    /// own.</summary>
    Task<GateOfflineConfig> GetOfflineConfigAsync(
        Guid operatorUserId, CancellationToken cancellationToken = default);

    Task<GateScanResult> RecordScanAsync(
        GateScanContext context, CancellationToken cancellationToken = default);

    Task<OperatorDailyReport> GetMyDailyReportAsync(
        Guid operatorUserId, Guid? gateId,
        CancellationToken cancellationToken = default);

    /// <summary>Cursor-paged list of scans at a single gate, for
    /// the staff app's "who's at this gate" view (SIMF-API-GATES-001 §7.4).
    /// Returns null when the operator is not assigned to the gate
    /// (handled as 403 at the endpoint).</summary>
    Task<GateVisitorsListResult> ListGateVisitorsAsync(
        Guid operatorUserId, Guid gateId, GateVisitorsListRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Result envelope for
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

/// <summary>The per-request envelope the engine consumes. Carries
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

/// <summary>The engine's result envelope.</summary>
public sealed record GateScanResult(
    GateScanResultKind Kind,
    GateScanResponse Response,
    bool IsIdempotentReplay,
    string? FailureReasonForHttp);

/// <remarks>
/// DEF-STF-008 — value <c>3</c> (the old <c>GateInactive</c> → HTTP 503
/// GATE_INACTIVE) is retired and its integer stays reserved. A scan at an
/// inactive gate is NOT a routing failure: the engine records it as a real
/// <see cref="SIMF.Common.Enums.DenialReasonCode.GateInactiveAtScan"/> denial
/// at HTTP 200 (GateOperatorService step 5), which keeps the append-only
/// <c>GateScan</c> audit row for the attempt and gives the operator the
/// designed red denial card carrying the server's bilingual
/// "This gate is currently inactive." No code path ever produced the 503, so
/// the endpoint arm that handled it was unreachable. See
/// SIMF-API-GATES-001 §7.2.4 / §8.1 (as-built note).
/// </remarks>
public enum GateScanResultKind
{
    /// <summary>Recorded scan (Allowed or Denied) — HTTP 200.</summary>
    Recorded = 0,
    /// <summary>HTTP 404 GATE_NOT_FOUND.</summary>
    GateNotFound = 1,
    /// <summary>HTTP 403 GATE_OPERATOR_NOT_ASSIGNED.</summary>
    NotAssigned = 2,
    /// <summary>HTTP 409 IDEMPOTENCY_KEY_CONFLICT.</summary>
    IdempotencyConflict = 4,
    /// <summary>HTTP 429 GATE_FAILURE_CIRCUIT_OPEN.</summary>
    CircuitOpen = 5,
}
