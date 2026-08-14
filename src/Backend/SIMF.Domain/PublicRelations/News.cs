using SIMF.Domain.Common;

namespace SIMF.Domain.PublicRelations;

/// <summary>
/// One news article, surfaced on the website, in the Control Panel and in the
/// app's news feed.
/// </summary>
public class News : BaseAuditEntity
{
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;

    /// <summary>The teaser on the news card. Null when the editor leaves it
    /// blank, and the app then derives one from the body.</summary>
    public string? Excerpt { get; set; }
    public string? ExcerptArabic { get; set; }

    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    /// <summary>The kicker above the title, such as "NAVAL" or "EVENTS". Held as
    /// inline text rather than as a lookup, and it can be promoted to a reference
    /// table later without changing the public payload.</summary>
    public string Category { get; set; } = string.Empty;
    public string CategoryArabic { get; set; } = string.Empty;

    /// <summary>The article's hero image, as its row in the one file store. A real foreign key:
    /// both sides live in the App database.
    ///
    /// <para>This was <c>ImageRelativePath</c>, admin-typed free text. An uploaded image and
    /// a linked one are now the same thing, a <c>StoredFile</c>, so the value is
    /// validated and stored once instead of living untyped on this row.</para>
    /// </summary>
    public Guid? ImageFileId { get; set; }

    /// <summary>The article date, and the gate for public visibility: a public
    /// read requires it to be in the past, so an article dated ahead is authored
    /// but not yet shown. The admin grid ignores it and shows every row. Also
    /// drives the newest-first public ordering.</summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>Ascending tie-breaker for articles sharing a publish
    /// instant.</summary>
    public int DisplayOrder { get; set; }
}
