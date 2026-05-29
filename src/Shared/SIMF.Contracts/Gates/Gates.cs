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
    public DateTimeOffset? ClientScannedAtUtc { get; set; }
    public string? IdempotencyKey { get; set; }
    public ScanSource Source { get; set; } = ScanSource.MobileApp;
}

/// <summary>Response from `POST /api/v1/gates/{gateId}/scans`
/// (SIMF-API-GATES-001 §7.2.1 / §7.2.2).</summary>
public sealed record GateScanResponse(
    long ScanId,
    ScanOutcome Outcome,
    ScanDirection Direction,
    DateTimeOffset ScannedAtUtc,
    GateScanUserProfile? UserProfile,
    DenialReasonCode? DenialReasonCode,
    string? DenialMessage);

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
    DateTimeOffset ScannedAtUtc,
    ScanOutcome Outcome,
    ScanDirection Direction,
    string? VisitorDisplayName,
    DenialReasonCode? DenialReasonCode);

/// <summary>`GET /api/v1/gates/my-reports/today` response.</summary>
public sealed record OperatorDailyReport(
    Guid OperatorUserId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    OperatorDailyReportTotals Totals,
    IReadOnlyList<OperatorDenialBucket> DenialBreakdown,
    IReadOnlyList<OperatorScanRow> Rows);

public sealed record OperatorDailyReportTotals(int Allowed, int Denied);
