using SIMF.Common.Enums;

namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Gates grid (D-148 — SIMF-API-GATES-001 §6.1).</summary>
public sealed record AdminGateSummary(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    DirectionMode DirectionMode,
    int AllowedProfileTypeCount,
    int AssignedOperatorCount,
    bool IsActive,
    DateTimeOffset CreatedAt,
    // D-506 — carried so the grid Excel export can round-trip the bilingual
    // description (not rendered as grid columns). Optional; blank when unset.
    string? Description = null,
    string? DescriptionArabic = null);

/// <summary>Full gate detail — Details + Edit modals
/// (SIMF-API-GATES-001 §6.2).</summary>
public sealed record AdminGateDetail(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    string? Description,
    string? DescriptionArabic,
    DirectionMode DirectionMode,
    bool IsActive,
    IReadOnlyList<Guid> AllowedProfileTypeIds,
    IReadOnlyList<Guid> AssignedOperatorUserIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    // X-1 — the hall this gate is a door for (null = perimeter gate). Appended
    // with a default so the CP-only contract stays append-only.
    Guid? HallId = null);

public sealed class AdminCreateGateRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public DirectionMode DirectionMode { get; set; } = DirectionMode.Both;
    // X-1 — optional hall-door binding (null = perimeter gate).
    public Guid? HallId { get; set; }
    public List<Guid> AllowedProfileTypeIds { get; set; } = new();
    public List<Guid> AssignedOperatorUserIds { get; set; } = new();
}

public sealed class AdminUpdateGateRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public DirectionMode DirectionMode { get; set; } = DirectionMode.Both;
    public bool IsActive { get; set; } = true;
    // X-1 — optional hall-door binding (null = perimeter gate).
    public Guid? HallId { get; set; }
    public List<Guid> AllowedProfileTypeIds { get; set; } = new();
    public List<Guid> AssignedOperatorUserIds { get; set; } = new();
}

/// <summary>One row in `GET /api/v1/admin/gates/{id}/assignments`
/// (SIMF-API-GATES-001 §6.7).</summary>
public sealed record AdminGateAssignmentRow(
    Guid AssignmentId,
    Guid UserId,
    string UserDisplayName,
    DateTimeOffset AssignedAt,
    Guid AssignedByUserId);

/// <summary>Filter for `GET /api/v1/admin/gates/reports/scans`
/// (SIMF-API-GATES-001 §6.8).</summary>
public sealed class AdminGateScanReportFilter
{
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public Guid? GateId { get; set; }
    public ScanOutcome? Outcome { get; set; }
    public int Skip { get; set; }
    public int Top { get; set; } = 50;
}

/// <summary>One row of the admin scan report.</summary>
public sealed record AdminGateScanRow(
    long ScanId,
    Guid GateId,
    string GateCode,
    Guid? UserProfileId,
    string? VisitorDisplayName,
    string QrIdAtScan,
    ScanDirection Direction,
    ScanOutcome Outcome,
    DenialReasonCode? DenialReasonCode,
    DateTimeOffset ScannedAtUtc,
    Guid ScannedByUserId,
    ScanSource Source);

/// <summary>One row of the "currently inside" view
/// (SIMF-API-GATES-001 §6.8 /currently-inside endpoint).</summary>
public sealed record AdminCurrentlyInsideRow(
    Guid UserProfileId,
    string DisplayName,
    string DisplayNameArabic,
    Guid? ProfileTypeId,
    string? ProfileTypeName,
    string? ProfileTypePageColor,
    DateTimeOffset LastCheckInAtUtc,
    Guid LastCheckInGateId,
    string LastCheckInGateCode);
