using SIMF.Common.Enums;

namespace SIMF.Contracts.Gates;

/// <summary>One row in `GET /api/v1/gates/my-assignments`
/// (SIMF-API-GATES-001 §7.1).</summary>
public sealed record OperatorGateAssignment(
    Guid GateId,
    string Code,
    string Name,
    string NameArabic,
    DirectionMode DirectionMode,
    bool IsActive);

/// <summary>`POST /api/v1/gates/{gateId}/scans` body
/// (SIMF-API-GATES-001 §7.2).</summary>
public sealed class GateScanRequest
{
    public string Qr { get; set; } = string.Empty;
    public DateTime? ClientScannedAt { get; set; }
    public string? IdempotencyKey { get; set; }
    public ScanSource Source { get; set; } = ScanSource.MobileApp;

    /// <summary>The operator's chosen movement direction (the
    /// دخول/خروج toggle on the staff console). Honoured ONLY when the gate's
    /// <see cref="DirectionMode"/> is <c>Both</c> (a dual-direction gate the
    /// operator can switch in/out without a CP change); a fixed In / Out gate
    /// ignores it and records its configured direction. Null = the server
    /// infers direction from the holder's last allowed scan (legacy behaviour).</summary>
    public ScanDirection? RequestedDirection { get; set; }
}

/// <summary>Response from `POST /api/v1/gates/{gateId}/scans`
/// (SIMF-API-GATES-001 §7.2.1 / §7.2.2).</summary>
/// <param name="NoticeMessage">DEF-CHK-004 — an ADVISORY note about a scan that
/// was still ALLOWED, already resolved to the caller's Accept-Language exactly
/// like <c>DenialMessage</c>. Today it carries the case the operator could not
/// otherwise see: a hall-door gate scan that recorded no hall attendance — no
/// session was live in the hall, or a check-out found no open row to close — so
/// entry was granted but nothing is being counted. Null on every other scan.
/// Append-only addition to the shipped wire contract — it never changes the
/// allow/deny outcome.</param>
public sealed record GateScanResponse(
    long ScanId,
    ScanOutcome Outcome,
    ScanDirection Direction,
    DateTime ScannedAt,
    GateScanUserProfile? UserProfile,
    DenialReasonCode? DenialReasonCode,
    string? DenialMessage,
    string? NoticeMessage = null);

public sealed record GateScanUserProfile(
    Guid Id,
    string DisplayName,
    string DisplayNameArabic,
    Guid? ProfileTypeId,
    string? ProfileTypeName,
    string? ProfileTypePageColor);

/// <summary>One denial-breakdown bucket in the operator's daily report
/// (SIMF-API-GATES-001 §7.3).</summary>
public sealed record OperatorDenialBucket(string Code, int Count);

/// <summary>One row in the operator daily-report grid.</summary>
public sealed record OperatorScanRow(
    long ScanId,
    DateTime ScannedAt,
    ScanOutcome Outcome,
    ScanDirection Direction,
    string? VisitorDisplayName,
    DenialReasonCode? DenialReasonCode);

/// <summary>`GET /api/v1/gates/my-reports/today` response.</summary>
public sealed record OperatorDailyReport(
    Guid OperatorUserId,
    DateTime FromUtc,
    DateTime ToUtc,
    OperatorDailyReportTotals Totals,
    IReadOnlyList<OperatorDenialBucket> DenialBreakdown,
    IReadOnlyList<OperatorScanRow> Rows);

public sealed record OperatorDailyReportTotals(int Allowed, int Denied);

/// <summary>`POST /api/v1/gates/{gateId}/visitors/list` body
/// (SIMF-API-GATES-001 §7.4). Cursor-paged view of scans recorded at
/// a single gate. The staff app polls with the previous response's
/// <see cref="GateVisitorsListResponse.NextCursor"/> to fetch new
/// arrivals since the last poll; first call sends a null cursor.</summary>
public sealed class GateVisitorsListRequest
{
    /// <summary>Opaque cursor returned by the previous response. Null
    /// on the first call; null returned in <c>NextCursor</c> means there
    /// are no more rows.</summary>
    public string? Cursor { get; set; }

    /// <summary>Client-requested page size. The server clamps this to
    /// 1..200 to bound memory; defaults to 50 when zero/negative.</summary>
    public int PageSize { get; set; }

    /// <summary>Optional filter — null means "any direction".</summary>
    public ScanDirection? Direction { get; set; }

    /// <summary>Optional filter. Defaults to <see cref="ScanOutcome.Allowed"/>
    /// when null so the typical "who's currently inside" use case stays
    /// the default. Pass <c>Denied</c> or roll your own client-side
    /// merge for "all".</summary>
    public ScanOutcome? Outcome { get; set; }

    /// <summary>Optional zone-free ISO-8601 lower bound, Saudi local (inclusive).</summary>
    public DateTime? Since { get; set; }

    /// <summary>Optional zone-free ISO-8601 upper bound, Saudi local (exclusive).</summary>
    public DateTime? Until { get; set; }
}

/// <summary>One item in <see cref="GateVisitorsListResponse"/>.
/// Snapshot fields (<see cref="DisplayName"/>, <see cref="ProfileTypeName"/>)
/// come from the D-158 frozen columns on <c>GateScan</c>; no cross-DB
/// JOIN to Identity is needed. PII (email / national-id / phone) is
/// intentionally NOT in this list view — fetch it from the per-scan
/// detail endpoint when an operator taps a row.</summary>
public sealed record GateVisitorListItem(
    long ScanId,
    DateTime ScannedAt,
    ScanDirection Direction,
    ScanOutcome Outcome,
    Guid? UserProfileId,
    string QrIdAtScan,
    string? DisplayName,
    string? ProfileTypeName,
    DenialReasonCode? DenialReasonCode);

/// <summary>Response body for
/// `POST /api/v1/gates/{gateId}/visitors/list`. The cursor is opaque
/// to the client. <see cref="AsOf"/> is the server clock at query
/// time — staff apps use it to detect clock skew and to display "last
/// refreshed N seconds ago" without inferring from item timestamps.</summary>
public sealed record GateVisitorsListResponse(
    IReadOnlyList<GateVisitorListItem> Items,
    string? NextCursor,
    DateTime AsOf);
