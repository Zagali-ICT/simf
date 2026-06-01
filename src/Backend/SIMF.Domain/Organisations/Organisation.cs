namespace SIMF.Domain.Organisations;

/// <summary>
/// A Saudi company / organisation row in the bilingual organisations lookup.
/// Populated by the government Excel import (CP "Import" affordance) and also
/// editable one-by-one from the Control Panel. Bilingual like
/// <c>Booth</c> (NameAr / NameEn), soft-deleted via <see cref="IsActive"/>,
/// and audited through the row-audit trail by the admin service.
///
/// <para><see cref="CommercialRegistration"/> is the company's commercial
/// registration number (سجل تجاري). Optional, but unique when present so a
/// re-import of the same gov sheet updates the existing row rather than
/// inserting a duplicate.</para>
/// </summary>
public class Organisation
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Arabic organisation name (1–256 chars). Required — the
    /// primary display name in the bilingual lookup.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>English organisation name (≤ 256 chars). Optional.</summary>
    public string? NameEn { get; set; }

    /// <summary>Commercial registration number / سجل تجاري (≤ 32 chars).
    /// Optional, but unique across organisations when present so a re-import
    /// updates the matching row instead of duplicating it.</summary>
    public string? CommercialRegistration { get; set; }

    /// <summary>Business sector / activity (≤ 128 chars), e.g.
    /// "Defense Systems". Optional.</summary>
    public string? Sector { get; set; }

    /// <summary>City the organisation is based in (≤ 128 chars). Optional.</summary>
    public string? City { get; set; }

    /// <summary>Contact phone number (≤ 32 chars). Optional.</summary>
    public string? Phone { get; set; }

    /// <summary>Contact e-mail address (≤ 320 chars). Optional.</summary>
    public string? Email { get; set; }

    /// <summary>Organisation website URL (≤ 512 chars). Optional.</summary>
    public string? Website { get; set; }

    /// <summary>Soft-delete flag. List/read/search endpoints filter on this.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Reverts <see cref="IsActive"/> to false — the soft-delete
    /// operation used by the admin DELETE endpoint.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>UTC timestamp the row was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC timestamp the row was last updated. Null until first edit.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
