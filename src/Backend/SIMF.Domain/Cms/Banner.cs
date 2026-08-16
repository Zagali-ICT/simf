using SIMF.Domain.Common;

namespace SIMF.Domain.Cms;

/// <summary>A time-windowed banner on the website and in the app; the public endpoint serves only active rows whose window contains the present moment.</summary>
public sealed class Banner : BaseAuditEntity
{
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    public Guid? ImageFileId { get; set; }

    /// <summary>Navigation rather than media, so it stays a plain URL instead of a file-store row.</summary>
    public string? LinkUrl { get; set; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    /// <summary>Zero is top, tie-broken by <see cref="Start"/>.</summary>
    public int DisplayOrder { get; set; }
}
