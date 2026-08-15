using SIMF.Domain.Common;

namespace SIMF.Domain.PublicRelations;

/// <summary>One news article, surfaced on the website, in the Control Panel and in the app's feed.</summary>
public class News : BaseAuditEntity
{
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;

    /// <summary>Null when the editor leaves it blank; the app then derives one from the body.</summary>
    public string? Excerpt { get; set; }
    public string? ExcerptArabic { get; set; }

    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    /// <summary>The kicker above the title, such as "NAVAL". Inline text, not a lookup.</summary>
    public string Category { get; set; } = string.Empty;
    public string CategoryArabic { get; set; } = string.Empty;

    public Guid? ImageFileId { get; set; }

    /// <summary>Gates public visibility: a public read requires it to be in the past, so an
    /// article dated ahead is authored but not shown. The admin grid ignores it.</summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>Ascending tie-breaker for articles sharing a publish instant.</summary>
    public int DisplayOrder { get; set; }
}
