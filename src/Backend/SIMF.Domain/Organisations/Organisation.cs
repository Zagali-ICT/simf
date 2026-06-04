using SIMF.Domain.Common;

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
public class Organisation: BaseAuditEntity
{
    /// <summary>English organisation name (≤ 256 chars). Optional.</summary>
    public string? Name { get; set; }
    
    /// <summary>Arabic organisation name (1–256 chars). Required — the
    /// primary display name in the bilingual lookup.</summary>
    public string NameArabic { get; set; } = string.Empty;

    
    /// <summary>Commercial registration number / سجل تجاري (≤ 32 chars).
    /// Optional, but unique across organisations when present so a re-import
    /// updates the matching row instead of duplicating it.</summary>
    public string? CommercialRegistration { get; set; }//long 700 CR700Id

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
}
