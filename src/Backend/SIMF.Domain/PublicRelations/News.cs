using SIMF.Domain.Common;

namespace SIMF.Domain.PublicRelations;

/// <summary>
/// D-199 (gap-list event module, Mockup screen 29 / 29b) — one News /
/// Media-Centre article surfaced on the Website, the Control Panel, and the
/// Flutter app's News feed. Public-relations / marketing owned content.
/// <para>
/// Carries the full bilingual set the mockup shows: a title, an optional short
/// excerpt (the card teaser on screen 29), the full body (screen 29b), a
/// bilingual category kicker (e.g. "NAVAL" / "EVENTS"), and an optional hero
/// image. <see cref="PublishedAt"/> is the article date shown in the UI and
/// the gate for public visibility — an article whose <see cref="PublishedAt"/>
/// is still in the future is authored but not yet shown publicly.
/// </para>
/// <para>
/// Mirrors the lookup-style soft-delete + audit-timestamp conventions used by
/// <c>Delegation</c> / <c>Country</c> / <c>Speaker</c>: a plain
/// <see cref="IsActive"/> flag flipped by the admin service (no domain
/// <c>Deactivate()</c> method — matches the sibling entities), and
/// <see cref="CreatedAt"/> / <see cref="UpdatedAt"/> timestamps stamped by the
/// service via <c>TimeProvider</c>.
/// </para>
/// </summary>
public class News : BaseAuditEntity
{
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;

    /// <summary>Optional short teaser shown on the news card (Mockup screen 29
    /// "news-excerpt"). Null when the editor leaves it blank; the app can then
    /// derive a teaser from the body.</summary>
    public string? Excerpt { get; set; }
    public string? ExcerptArabic { get; set; }

    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    /// <summary>Bilingual category kicker shown above the title
    /// (e.g. "NAVAL" / "EVENTS" in the mockup). Kept as inline text rather
    /// than a lookup FK to stay within the single-table D-199 scope; it can be
    /// promoted to a reference table later without changing the public payload
    /// shape.</summary>
    public string Category { get; set; } = string.Empty;
    public string CategoryArabic { get; set; } = string.Empty;

    /// <summary>Optional hero / card image, stored as a relative path under the
    /// configured media root (same convention as
    /// <c>Speaker.PhotoRelativePath</c>). The upload pipeline itself is out of
    /// this module's scope — this field stores / echoes the path only.</summary>
    public string? ImageRelativePath { get; set; }

    /// <summary>The article date (UI "news-date") and the public-visibility
    /// gate: public reads require <c>PublishedAt &lt;= now</c>; the admin grid
    /// shows every row regardless. Drives the "newest first" public ordering.</summary>
    public DateTimeOffset PublishedAt { get; set; }

    /// <summary>Tie-breaker for items sharing a publish instant (ascending).</summary>
    public int DisplayOrder { get; set; }
}
