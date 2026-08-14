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

    /// <summary>The banner image, as its row in the one file store. A real
    /// foreign key: both sides live in the App database.
    ///
    /// <para>This was <c>ImageUrl</c>, free text an editor pasted, and the app
    /// loaded it directly. An uploaded image and a linked one are now the same
    /// thing — a <c>StoredFile</c>, the linked case carrying
    /// <c>SourceType.ExternalLink</c> — so the pasted URL is validated and stored
    /// once rather than living untyped on this row.</para></summary>
    public Guid? ImageFileId { get; set; }

    /// <summary>The click-through target. Free text, and deliberately still a
    /// URL: it is navigation, not media, so the file store has nothing to say
    /// about it.</summary>
    public string? LinkUrl { get; set; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    /// <summary>Zero is top, tie-broken by <see cref="Start"/>.</summary>
    public int DisplayOrder { get; set; }
}
