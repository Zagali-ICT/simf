using SIMF.Domain.Common;

namespace SIMF.Domain.Feedback;

/// <summary>One admin-defined rating question, always scored on a fixed 1–5 star scale.
/// A null <see cref="RatingQuestionGroupId"/> renders it in the flat, ungrouped list.</summary>
public sealed class RatingQuestion : BaseAuditEntity
{
    public Guid RatingTypeId { get; set; }
    public RatingType? Type { get; set; }

    public Guid? RatingQuestionGroupId { get; set; }
    public RatingQuestionGroup? Group { get; set; }

    public string Text { get; set; } = string.Empty;

    public string TextArabic { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public int DisplayOrder { get; set; }
}
