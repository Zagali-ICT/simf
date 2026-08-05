namespace SIMF.Domain.Cms;

/// <summary>
/// One editable piece of public content — a welcome message, a page heading,
/// body copy, a label. It is identified by a stable <see cref="Key"/> the clients
/// code against, and an admin edits both languages at runtime without a code
/// change.
///
/// <para>The app and the website read these through the public content endpoint
/// and cache locally, revalidating against <see cref="LastUpdatedAt"/>.</para>
/// </summary>
public sealed class ContentBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Lower-kebab-case with a dotted hierarchy, such as
    /// <c>home.welcome.title</c>. Unique, and renaming one is a breaking change
    /// for every client coded against it.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Rendered as plain text, never as markup. It is admin-editable, so
    /// rendering it as markup would mean rendering admin-supplied HTML; every
    /// consumer emits it through Razor's auto-encoding. A renderer added later
    /// would have to sanitise first. Long enough that an article body needs no
    /// separate table.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Rendered as plain text, on the same terms as
    /// <see cref="Content"/>.</summary>
    public string ContentArabic { get; set; } = string.Empty;

    /// <summary>Hides the row from the public endpoint without losing the
    /// editor's text.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The admin who last saved the row. A bare Guid: the user lives in
    /// the Identity database.</summary>
    public Guid LastUpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUpdatedAt { get; set; }
}
