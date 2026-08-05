using SIMF.Domain.Common;

namespace SIMF.Domain.Organization;

/// <summary>
/// One "about" item on an <see cref="OrganizationProfile"/> — the forum's
/// mission or vision, for instance — rendered in the app as a card and ordered
/// within its profile.
/// </summary>
public sealed class OrganizationAboutItem : BaseAuditEntity
{
    /// <summary>A real foreign key, since the profile lives in the same
    /// database.</summary>
    public Guid OrganizationProfileId { get; set; }
    public OrganizationProfile? Profile { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Paired with <see cref="Title"/>.</summary>
    public string TitleArabic { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    /// <summary>Paired with <see cref="Text"/>.</summary>
    public string TextArabic { get; set; } = string.Empty;

    /// <summary>Ascending, within the owning profile.</summary>
    public int DisplayOrder { get; set; }
}
