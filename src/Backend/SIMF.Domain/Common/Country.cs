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





    /// <summary>D-473 (#10) — true for a country invited to send a delegation
    /// (وفد); a delegate's nationality must be an invited country. Additive,
    /// defaults false (the admin marks the invited countries in the CP).</summary>
    public bool IsInvited { get; set; }

    /// <summary>D-499 (Figma 1426:10771 الوفود) — the invited delegation's
    /// arrival date for this event. Additive nullable; set by the admin on the
    /// CP country form alongside <see cref="IsInvited"/>. Null until supplied.</summary>
    public DateOnly? DelegationArrivalDate { get; set; }

    /// <summary>D-499 (الوفود) — the invited delegation's departure date.
    /// Additive nullable; rendered with the arrival date as the card's date
    /// range ("12 يناير – 15 يناير").</summary>
    public DateOnly? DelegationDepartureDate { get; set; }

    /// <summary>D-499 (الوفود) — the <see cref="Profiles.UserProfile"/> id of the
    /// delegate designated as this country's head of delegation (رئيس الوفد).
    /// Same-DB real FK to UserProfile (both on <c>SimfAppDbContext</c>, so D-157
    /// is not engaged); nullable, <c>OnDelete.SetNull</c>. Resolved on read to the
    /// head's name + job title. Null until the admin picks one.</summary>
    public Guid? HeadOfDelegationUserProfileId { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
