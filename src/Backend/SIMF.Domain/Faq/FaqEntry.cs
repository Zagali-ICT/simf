using SIMF.Domain.Common;

namespace SIMF.Domain.Faq;

/// <summary>
/// One question-and-answer pair inside a <see cref="FaqGroup"/>.
/// </summary>
public sealed class FaqEntry : BaseAuditEntity
{
    /// <summary>A real foreign key, since the group lives in the same
    /// database.</summary>
    public Guid FaqGroupId { get; set; }
    public FaqGroup? Group { get; set; }

    public string Question { get; set; } = string.Empty;

    /// <summary>Paired with <see cref="Question"/>.</summary>
    public string QuestionArabic { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    /// <summary>Paired with <see cref="Answer"/>.</summary>
    public string AnswerArabic { get; set; } = string.Empty;

    /// <summary>Ascending, within the owning group.</summary>
    public int DisplayOrder { get; set; }
}
