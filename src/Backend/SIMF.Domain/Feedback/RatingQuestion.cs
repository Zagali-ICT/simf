using SIMF.Domain.Common;

namespace SIMF.Domain.Feedback;

/// <summary>
/// One admin-defined rating question. Every question is a fixed 1–5 star scale
/// (the owner's "all rate has fix from 1-5"); the wording is data. A question
/// belongs to a <see cref="RatingType"/> and optionally to a
/// <see cref="RatingQuestionGroup"/> (null = flat / ungrouped). Bilingual,
/// orderable, soft-deletable.
/// </summary>
public sealed class RatingQuestion : BaseAuditEntity
{
    /// <summary>Owning rating type (real DB FK — same DbContext).</summary>
    public Guid RatingTypeId { get; set; }
    public RatingType? Type { get; set; }

    /// <summary>Optional owning group (real DB FK, nullable). Null renders the
    /// question in the flat / ungrouped list.</summary>
    public Guid? RatingQuestionGroupId { get; set; }
    public RatingQuestionGroup? Group { get; set; }

    /// <summary>English question text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Arabic question text — paired with <see cref="Text"/>.</summary>
    public string TextArabic { get; set; } = string.Empty;

    /// <summary>When true the attendee must score this question (1–5) before the
    /// submission is accepted.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Sort key within the type / group (ascending).</summary>
    public int DisplayOrder { get; set; }
}
