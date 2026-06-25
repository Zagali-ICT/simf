using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Feedback;

/// <summary>
/// A configurable rating "context" the admin defines (e.g. "App", "Session").
/// Replaces the old fixed single-row <c>Rating</c> model: questions, groups and
/// the comment toggle are all data now, not schema. Mirrors the FAQ module's
/// bilingual / orderable / soft-deletable CRUD shape (see <c>FaqGroup</c>).
/// <para>
/// <see cref="Scope"/> decides how many times a user may submit it:
/// <see cref="RatingScope.Global"/> = once per user (App); <see cref="RatingScope.PerSession"/>
/// = once per user per target session. <see cref="IsSystem"/> types (App + Session)
/// are seeded and cannot be deleted, and their <see cref="Code"/> is locked.
/// </para>
/// </summary>
public sealed class RatingType : BaseAuditEntity
{
    /// <summary>Stable unique slug used by the app + worker to resolve a type
    /// without knowing its Guid (e.g. <c>"App"</c>, <c>"Session"</c>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>English display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name — paired with <see cref="Name"/>.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Whether the type is a once-per-user global survey or a
    /// per-session one. Drives <c>RatingResponse.TargetId</c>.</summary>
    public RatingScope Scope { get; set; }

    /// <summary>When true the form shows a single overall 1–5 star bar above the
    /// questions (the classic "Rate the Forum" overall). When false the overall
    /// is omitted and only the per-question scores are collected.</summary>
    public bool HasOverallStars { get; set; } = true;

    /// <summary>When true the form shows an optional free-text comment box at the
    /// end (the owner's "allow txt or not at end" toggle).</summary>
    public bool AllowComment { get; set; } = true;

    /// <summary>Optional English label for the comment box; null falls back to a
    /// generic localized "Comments (optional)".</summary>
    public string? CommentLabel { get; set; }

    /// <summary>Optional Arabic comment-box label — paired with <see cref="CommentLabel"/>.</summary>
    public string? CommentLabelArabic { get; set; }

    /// <summary>Seeded system type (App / Session). System types can't be deleted
    /// and their <see cref="Code"/> can't be changed.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Sort key across the rating-type list (ascending).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>The type's question groups (Speaker / Sound / Light …).</summary>
    public ICollection<RatingQuestionGroup> Groups { get; set; } = new List<RatingQuestionGroup>();

    /// <summary>The type's questions (grouped or flat — see
    /// <see cref="RatingQuestion.RatingQuestionGroupId"/>).</summary>
    public ICollection<RatingQuestion> Questions { get; set; } = new List<RatingQuestion>();
}
