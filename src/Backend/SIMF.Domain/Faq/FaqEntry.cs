using SIMF.Domain.Common;

namespace SIMF.Domain.Faq;

/// <summary>One question-and-answer pair inside a <see cref="FaqGroup"/>.</summary>
public sealed class FaqEntry : BaseAuditEntity
{
    public Guid FaqGroupId { get; set; }
    public FaqGroup? Group { get; set; }

    public string Question { get; set; } = string.Empty;
    public string QuestionArabic { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;
    public string AnswerArabic { get; set; } = string.Empty;

    /// <summary>Ascending, within the owning group.</summary>
    public int DisplayOrder { get; set; }
}
