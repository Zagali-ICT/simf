using SIMF.Domain.Common;

namespace SIMF.Domain.Contacts;
//this common
/// <summary>
/// SIMF-FDS-014 (D-260) — a shared, de-duplicated contact / party directory
/// record. The "identity-card" cluster (logo, bilingual name, phones, social
/// links, website, map location, country) is extracted into one row that the
/// org-facing entities (Company, Sponsor, MediaPartner, Speaker, and the Booth
/// officer) reference by a nullable <c>ContactId</c> FK. One Contact may be
/// reused across roles — editing it updates every entity that links it.
///
/// <para>Soft-deleted via <see cref="IsActive"/>. A Contact still referenced by
/// an active entity must not be deactivated — that guard is enforced by the
/// contact service (a later slice), not by the schema. <see cref="CountryId"/>
/// is a same-DB FK to <c>SIMF.Domain.Common.Country.Id</c> (the bilingual ISO
/// lookup). <see cref="LogoRelativePath"/> is a relative path (never an absolute
/// URL), matching Sponsor / MediaPartner / Speaker.</para>
/// </summary>
public sealed class Contact : BaseAuditEntity
{
    /// <summary>English display name (≤256 chars). Optional.</summary>
    public string? Name { get; set; }
    /// <summary>Arabic display name (1–256 chars) — the primary surface.</summary>
    public string NameArabic { get; set; } = string.Empty;






    /// <summary>Primary contact phone (≤32 chars). Optional.</summary>
    public string? PhonePrimary { get; set; }

    /// <summary>Secondary contact phone (≤32 chars). Optional.</summary>
    public string? PhoneSecondary { get; set; }

    /// <summary>Contact e-mail address (≤320 chars). Optional.</summary>
    public string? Email { get; set; }

    /// <summary>Website URL (≤512 chars). Optional.</summary>
    public string? Website { get; set; }

    /// <summary>Facebook profile URL (≤256 chars). Optional.</summary>
    public string? FacebookUrl { get; set; }

    /// <summary>X (formerly Twitter) profile URL (≤256 chars). Optional.</summary>
    public string? XUrl { get; set; }

    /// <summary>LinkedIn profile URL (≤256 chars). Optional.</summary>
    public string? LinkedInUrl { get; set; }

    /// <summary>Instagram profile URL (≤256 chars). Optional.</summary>
    public string? InstagramUrl { get; set; }


    /// <summary>ISO 3166-1 numeric country id — same-DB FK to
    /// <c>SIMF.Domain.Common.Country.Id</c>. Optional.</summary>
    public int? CountryId { get; set; }

    /// <summary>Wave 3 (Figma 1439:11881 / 11826) — the city, shown on the
    /// exhibitor / sponsor detail location line ("الظهران، المملكة العربية
    /// السعودية" = City، Country). Bilingual; ≤128. Optional. Additive (D-219).</summary>
    public string? City { get; set; }
    public string? CityArabic { get; set; }

    /// <summary>Map latitude (WGS84, [-90,90]). Optional — set together with
    /// <see cref="Longitude"/>.</summary>
    public double? Latitude { get; set; }

    /// <summary>Map longitude (WGS84, [-180,180]). Optional — set together with
    /// <see cref="Latitude"/>.</summary>
    public double? Longitude { get; set; }


    /// <summary>Relative path to the contact's logo asset (≤512 chars), resolved
    /// against the static asset root. Optional — never an absolute URL.</summary>
    public string? LogoRelativePath { get; set; }



    
}
