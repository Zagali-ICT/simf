using SIMF.Domain.Common;

namespace SIMF.Domain.Organization;

/// <summary>
/// One labelled detail on an <see cref="OrganizationProfile"/>, such as
/// "Year : 2026" or "Location : Riyadh", rendered in the app as a name-and-value
/// list in <see cref="DisplayOrder"/>.
/// </summary>
public sealed class OrganizationDetail : BaseAuditEntity
{
    /// <summary>A real foreign key, since the profile lives in the same
    /// database.</summary>
    public Guid OrganizationProfileId { get; set; }
    public OrganizationProfile? Profile { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Paired with <see cref="Name"/>.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>The value as read in English: a year, a URL, an organiser
    /// name.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Optional, because a language-neutral value such as a year or a
    /// URL needs no translation and the app falls back to <see cref="Value"/>.
    /// A language-specific value such as an organiser's name sets it, so an
    /// Arabic reader sees Arabic.</summary>
    public string? ValueArabic { get; set; }

    /// <summary>Ascending, within the owning profile.</summary>
    public int DisplayOrder { get; set; }
}
