using SIMF.Domain.Common;

namespace SIMF.Domain.Organization;

/// <summary>
/// One labelled detail on an <see cref="OrganizationProfile"/>, such as "Year : 2026",
/// rendered in the app as a name-and-value list in <see cref="DisplayOrder"/>.
/// </summary>
public sealed class OrganizationDetail : BaseAuditEntity
{
    public Guid OrganizationProfileId { get; set; }
    public OrganizationProfile? Profile { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NameArabic { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    /// <summary>Optional: a language-neutral value such as a year or a URL needs no
    /// translation, and the app falls back to <see cref="Value"/>.</summary>
    public string? ValueArabic { get; set; }

    public int DisplayOrder { get; set; }
}
