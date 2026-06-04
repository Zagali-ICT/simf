namespace SIMF.Domain.Common;

/// <summary>
/// D-151 — country lookup. <see cref="Id"/> is the ISO 3166-1 **numeric**
/// code (manually assigned, NOT IDENTITY) so the same row carries the same
/// id across every SIMF environment and across regional partner systems.
/// <see cref="Code"/> is the ISO 3166-1 alpha-2 code ("SA", "AE", …) and is
/// the canonical lookup key when an external system only knows the
/// two-letter form. <see cref="PhonePrefix"/> is the E.164 country dial code
/// with the leading "+".
/// </summary>
public class Country
{
    /// <summary>ISO 3166-1 numeric code (e.g. 682 = SA, 784 = AE).
    /// Manually assigned in seed data — NOT IDENTITY — so admins can copy
    /// rows between environments without auto-increment drift.</summary>
    public int Id { get; set; }

    /// <summary>ISO 3166-1 alpha-2 code (e.g. "SA"). Unique.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>English country name (1–128 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic country name (1–128 chars).</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>E.164 country dial code with the leading "+" (≤ 8 chars,
    /// e.g. "+966", "+971"). Optional — some non-state territories don't
    /// have one.</summary>
    public string? PhonePrefix { get; set; }

    /// <summary>Sort key in the visitor-facing nationality picker
    /// (≥ 0; ascending). The seed leaves room (10, 20, 30 …) so admins
    /// can insert custom orderings without renumbering.</summary>
    public int DisplayOrder { get; set; }





    /// <summary>Soft-delete flag.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
