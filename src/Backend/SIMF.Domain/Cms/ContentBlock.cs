namespace SIMF.Domain.Cms;

/// <summary>
/// One admin-editable piece of public content, addressed by a stable <see cref="Key"/>.
/// The app and the website read these through the public content endpoint and cache
/// locally, revalidating against <see cref="LastUpdatedAt"/>.
/// </summary>
public sealed class ContentBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Lower-kebab-case with a dotted hierarchy, such as <c>home.welcome.title</c>. Renaming one breaks every client coded against it.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Rendered as plain text, never markup: it is admin-supplied, so a renderer added later would have to sanitise first.</summary>
    public string Content { get; set; } = string.Empty;

    public string ContentArabic { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>A bare Guid: the user lives in the Identity database.</summary>
    public Guid LastUpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUpdatedAt { get; set; }
}
