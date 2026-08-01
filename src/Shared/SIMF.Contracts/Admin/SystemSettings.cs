namespace SIMF.Contracts.Admin;

/// <summary>P2.4 — D-229 (FDS-012 §5.5): one system-setting row in the admin list.</summary>
public sealed record AdminSystemSettingSummary(
    Guid Id,
    string Key,
    string Value,
    string? Description,
    bool IsActive);

/// <summary>P2.4 — full system-setting detail.</summary>
public sealed record AdminSystemSettingDetail(
    Guid Id,
    string Key,
    string Value,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>P2.4 — create a system setting (key is fixed once created).</summary>
public sealed class AdminCreateSystemSettingRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>P2.4 — update a system setting's value / description / active flag.</summary>
public sealed class AdminUpdateSystemSettingRequest
{
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
