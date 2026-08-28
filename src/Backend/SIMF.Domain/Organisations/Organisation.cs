using SIMF.Domain.Common;

namespace SIMF.Domain.Organisations;

/// <summary>
/// A Saudi company in the bilingual organisations lookup, loaded by the government Excel
/// import and also editable one row at a time from the Control Panel.
/// </summary>
public class Organisation: BaseAuditEntity
{
    /// <summary>The catch-all row a visitor picks when their employer is not in
    /// the curated list, at which point they type it into
    /// <see cref="Profiles.UserProfile.OrganisationOther"/>.
    ///
    /// <para>A constant id rather than a lookup by name — the same reason
    /// <see cref="Badges.BadgeBatch.DirectRegistrationId"/> is one: the seeder
    /// stays idempotent and nothing depends on the display text staying put.
    /// The government Excel import matches on commercial registration, which
    /// this row has none of, so a re-import cannot collide with it.</para>
    /// </summary>
    public static readonly Guid OtherId =
        new("A17E9C42-0B6D-4F58-9E31-7C2A8D5F60B4");

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
