using SIMF.Domain.Common;

namespace SIMF.Domain.Organisations;

/// <summary>
/// A Saudi company in the bilingual organisations lookup, loaded by the government Excel
/// import and also editable one row at a time from the Control Panel.
/// </summary>
public class Organisation: BaseAuditEntity
{
    public string? Name { get; set; }

    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Commercial registration number (سجل تجاري); the import matches on it.</summary>
    public string? CommercialRegistration { get; set; }

    public string? Sector { get; set; }

    public string? City { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; } 
}
