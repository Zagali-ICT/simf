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

/// <summary>BUG-018 — one selectable gate-operator candidate
/// (`POST /api/v1/admin/gates/operator-candidates/list`). Gate scanning happens
/// through the mobile app, so a candidate is an approved APP account whose
/// profile type is operational (<c>IsForVisitor=false</c>) and carries a
/// <see cref="MobileAppRole"/> that confers <c>Gates.Operate</c> — never a
/// Control-Panel admin account.</summary>
public sealed record AdminGateOperatorCandidate(
    Guid UserId,
    string Email,
    string DisplayName,
    string ProfileTypeName,
    MobileAppRole MobileAppRole);

/// <summary>BUG-018 — one option in a gate-form lookup list.</summary>
public sealed record AdminGateLookupOption(Guid Id, string Name, string NameArabic);

/// <summary>BUG-018 — the lookup lists the gate Add/Edit form needs, served under
/// <c>Gates.Manage</c> (`GET /api/v1/admin/gates/form-options`). Previously the
/// form read the shared ProfileTypes / Halls admin lists, which need
/// <c>ProfileTypes.View</c> / <c>Halls.View</c> — so a Security-team gate manager
/// (who holds only <c>Gates.Manage</c>) saw silently empty dropdowns.</summary>
public sealed record AdminGateFormOptions(
    IReadOnlyList<AdminGateLookupOption> ProfileTypes,
    IReadOnlyList<AdminGateLookupOption> Halls);

/// <summary>One row in `GET /api/v1/admin/gates/{id}/assignments`
/// (SIMF-API-GATES-001 §6.7).</summary>
public sealed record AdminGateAssignmentRow(
    Guid AssignmentId,
    Guid UserId,
    string UserDisplayName,
    DateTimeOffset AssignedAt,
    Guid AssignedByUserId,
    // BUG-018 (18-6) — the gate detail view lists WHO is assigned, not just a
    // count, so an assignment can be audited from the CP. Appended with a default
    // so this CP-only contract stays append-only.
    string UserEmail = "");

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
    DateTimeOffset ScannedAt,
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
    DateTimeOffset LastCheckInAt,
    Guid LastCheckInGateId,
    string LastCheckInGateCode);
