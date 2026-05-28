namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Halls grid (D-134 Sprint B —
/// SIMF-FDS-004 §5.2).</summary>
public sealed record AdminHallSummary(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    int Capacity,
    string? Floor,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>Full hall detail (Details + Edit modals).</summary>
public sealed record AdminHallDetail(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    int Capacity,
    string? Floor,
    string? EquipmentNotes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class AdminCreateHallRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Floor { get; set; }
    public string? EquipmentNotes { get; set; }
}

public sealed class AdminUpdateHallRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Floor { get; set; }
    public string? EquipmentNotes { get; set; }
    public bool IsActive { get; set; } = true;
}
