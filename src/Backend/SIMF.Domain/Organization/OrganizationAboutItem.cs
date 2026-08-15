using SIMF.Domain.Common;

namespace SIMF.Domain.Organization;

/// <summary>
/// One "about" card on an <see cref="OrganizationProfile"/> — the forum's mission or
/// vision, for instance — ordered within its profile.
/// </summary>
public sealed class OrganizationAboutItem : BaseAuditEntity
{
    public Guid OrganizationProfileId { get; set; }
    public OrganizationProfile? Profile { get; set; }

    public string Title { get; set; } = string.Empty;

    public string TitleArabic { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string TextArabic { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}
