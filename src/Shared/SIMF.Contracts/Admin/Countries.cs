namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Countries grid (D-151).</summary>
public sealed record AdminCountrySummary(
    int Id,
    string Code,
    string Name,
    string NameArabic,
    string? PhonePrefix,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    // D-473 (#10) — invited to send a delegation (وفد).
    bool IsInvited = false);

/// <summary>Full country detail (Details + Edit modals).</summary>
public sealed record AdminCountryDetail(
    int Id,
    string Code,
    string Name,
    string NameArabic,
    string? PhonePrefix,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    // D-473 (#10) — invited to send a delegation (وفد).
    bool IsInvited = false);

public sealed class AdminCreateCountryRequest
{
    /// <summary>ISO 3166-1 numeric — manually assigned (e.g. 682 = SA).</summary>
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? PhonePrefix { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>D-473 (#10) — true for a country invited to send a delegation (وفد).</summary>
    public bool IsInvited { get; set; }
}

public sealed class AdminUpdateCountryRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? PhonePrefix { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>D-473 (#10) — true for a country invited to send a delegation (وفد).</summary>
    public bool IsInvited { get; set; }
}
