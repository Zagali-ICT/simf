namespace SIMF.Domain.Faq;

/// <summary>
/// P2.1 (D-211) — one question/answer pair inside a <see cref="FaqGroup"/>.
/// Bilingual, orderable within its group, soft-deletable.
/// </summary>
public sealed class FaqEntry
{
    public Guid Id { get; set; }

    /// <summary>Owning group (real DB FK — same DbContext).</summary>
    public Guid FaqGroupId { get; set; }
    public FaqGroup? Group { get; set; }

    /// <summary>English question.</summary>
    public string QuestionEn { get; set; } = string.Empty;

    /// <summary>Arabic question — paired with <see cref="QuestionEn"/>.</summary>
    public string QuestionAr { get; set; } = string.Empty;

    /// <summary>English answer (long text).</summary>
    public string AnswerEn { get; set; } = string.Empty;

    /// <summary>Arabic answer — paired with <see cref="AnswerEn"/>.</summary>
    public string AnswerAr { get; set; } = string.Empty;

    /// <summary>Sort key within the owning group (ascending).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
