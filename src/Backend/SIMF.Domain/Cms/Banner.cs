using SIMF.Domain.Common;

namespace SIMF.Domain.Cms;

/// <summary>
/// D-173 (gap doc G8, PDF §1) — one time-windowed banner / announcement
/// surfaced on the public Website and Flutter app. Editors set a
/// start / end window; the public read endpoint serves only the rows
/// where now ∈ [StartUtc, EndUtc] AND <see cref="IsActive"/>.
///
/// <para>Distinct from <see cref="ContentBlock"/> because banners
/// have a lifecycle (announcement → live → ended) and an explicit
/// display order; folding them into a key/value content block would
/// lose those columns to a stringified JSON blob.</para>
/// </summary>
public sealed class Banner : BaseAuditEntity
{
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    /// <summary>Optional banner image URL. Free text — the editor
    /// can paste an absolute URL or a relative content path served
    /// by the static-asset host.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Optional click-through URL. Same shape as
    /// <see cref="ImageUrl"/>.</summary>
    public string? LinkUrl { get; set; }

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    /// <summary>Display order — 0 = top. Tie-broken by
    /// <see cref="StartUtc"/>.</summary>
    public int DisplayOrder { get; set; }
}
