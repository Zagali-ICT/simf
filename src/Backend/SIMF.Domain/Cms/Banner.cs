using SIMF.Domain.Common;

namespace SIMF.Domain.Cms;

/// <summary>
/// A time-windowed banner on the website and in the app. Editors set a start and
/// end, and the public endpoint serves only the rows that are active and whose
/// window contains the present moment.
///
/// <para>Kept separate from <see cref="ContentBlock"/> because a banner has a
/// lifecycle and an explicit ordering; folding it into a key/value block would
/// bury those columns in a stringified blob.</para>
/// </summary>
public sealed class Banner : BaseAuditEntity
{
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    /// <summary>Free text, so an editor can paste either an absolute URL or a
    /// path served by the static-asset host.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>The click-through target, in the same shape as
    /// <see cref="ImageUrl"/>.</summary>
    public string? LinkUrl { get; set; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    /// <summary>Zero is top, tie-broken by <see cref="Start"/>.</summary>
    public int DisplayOrder { get; set; }
}
