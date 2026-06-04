namespace SIMF.Contracts.Contacts;

// SIMF-FDS-014 (D-261) — admin CRUD contracts for the shared, de-duplicated
// Contact directory (logo / bilingual name / phones / social / website /
// map lat-long / country). One Contact may be reused across roles (Company,
// Sponsor, MediaPartner, Speaker, Booth officer). Country names are projected
// onto the summary/detail so grids/forms need no second fetch. Mirrors the
// Organisation admin contracts.

/// <summary>One contact row in the admin grid.</summary>
public sealed record AdminContactSummary(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? PhonePrimary,
    string? Email,
    int? CountryId,
    string? CountryNameEn,
    string? CountryNameAr,
    bool IsActive);

/// <summary>Full contact detail — every column for the admin view/edit form.</summary>
public sealed record AdminContactDetail(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? LogoRelativePath,
    string? PhonePrimary,
    string? PhoneSecondary,
    string? Email,
    string? Website,
    string? FacebookUrl,
    string? XUrl,
    string? LinkedInUrl,
    string? InstagramUrl,
    double? Latitude,
    double? Longitude,
    int? CountryId,
    string? CountryNameEn,
    string? CountryNameAr,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Admin create payload.</summary>
public sealed class CreateContactRequest
{
    /// <summary>Arabic display name (required).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>English display name (optional).</summary>
    public string? NameEn { get; set; }

    /// <summary>Relative path to the logo asset (optional, never an absolute URL).</summary>
    public string? LogoRelativePath { get; set; }

    /// <summary>Primary contact phone (optional).</summary>
    public string? PhonePrimary { get; set; }

    /// <summary>Secondary contact phone (optional).</summary>
    public string? PhoneSecondary { get; set; }

    /// <summary>Contact e-mail (optional).</summary>
    public string? Email { get; set; }

    /// <summary>Website URL (optional).</summary>
    public string? Website { get; set; }

    /// <summary>Facebook profile URL (optional).</summary>
    public string? FacebookUrl { get; set; }

    /// <summary>X (Twitter) profile URL (optional).</summary>
    public string? XUrl { get; set; }

    /// <summary>LinkedIn profile URL (optional).</summary>
    public string? LinkedInUrl { get; set; }

    /// <summary>Instagram profile URL (optional).</summary>
    public string? InstagramUrl { get; set; }

    /// <summary>Map latitude — set together with <see cref="Longitude"/> (optional).</summary>
    public double? Latitude { get; set; }

    /// <summary>Map longitude — set together with <see cref="Latitude"/> (optional).</summary>
    public double? Longitude { get; set; }

    /// <summary>ISO 3166-1 numeric country id (optional FK to the Country lookup).</summary>
    public int? CountryId { get; set; }
}

/// <summary>Admin update payload.</summary>
public class UpdateContactRequest
{
    /// <summary>Arabic display name (required).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>English display name (optional).</summary>
    public string? NameEn { get; set; }

    /// <summary>Relative path to the logo asset (optional, never an absolute URL).</summary>
    public string? LogoRelativePath { get; set; }

    /// <summary>Primary contact phone (optional).</summary>
    public string? PhonePrimary { get; set; }

    /// <summary>Secondary contact phone (optional).</summary>
    public string? PhoneSecondary { get; set; }

    /// <summary>Contact e-mail (optional).</summary>
    public string? Email { get; set; }

    /// <summary>Website URL (optional).</summary>
    public string? Website { get; set; }

    /// <summary>Facebook profile URL (optional).</summary>
    public string? FacebookUrl { get; set; }

    /// <summary>X (Twitter) profile URL (optional).</summary>
    public string? XUrl { get; set; }

    /// <summary>LinkedIn profile URL (optional).</summary>
    public string? LinkedInUrl { get; set; }

    /// <summary>Instagram profile URL (optional).</summary>
    public string? InstagramUrl { get; set; }

    /// <summary>Map latitude — set together with <see cref="Longitude"/> (optional).</summary>
    public double? Latitude { get; set; }

    /// <summary>Map longitude — set together with <see cref="Latitude"/> (optional).</summary>
    public double? Longitude { get; set; }

    /// <summary>ISO 3166-1 numeric country id (optional FK to the Country lookup).</summary>
    public int? CountryId { get; set; }

    /// <summary>Whether the contact is active.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>Lightweight contact item for the CP "link existing contact" picker
/// on the Company / Sponsor / MediaPartner / Speaker / Booth admin forms.</summary>
public sealed record ContactPickerItem(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? LogoRelativePath);
