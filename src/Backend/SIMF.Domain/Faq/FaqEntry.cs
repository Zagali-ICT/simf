using SIMF.Domain.Common;

namespace SIMF.Domain.Faq;

/// <summary>
/// P2.1 (D-211) — one question/answer pair inside a <see cref="FaqGroup"/>.
/// Bilingual, orderable within its group, soft-deletable.
/// </summary>
public sealed class FaqEntry:BaseAuditEntity
{ 

    /// <summary>Owning group (real DB FK — same DbContext).</summary>
    public Guid FaqGroupId { get; set; }
    public FaqGroup? Group { get; set; }

    /// <summary>English question.</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>Arabic question — paired with <see cref="Question"/>.</summary>
    public string QuestionArabic { get; set; } = string.Empty;

    /// <summary>English answer (long text).</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>Arabic answer — paired with <see cref="Answer"/>.</summary>
    public string AnswerArabic { get; set; } = string.Empty;

    /// <summary>Sort key within the owning group (ascending).</summary>
    public int DisplayOrder { get; set; }

    
}
