namespace SIMF.Contracts.Faq;

// P2.1 (D-211) — FAQ management contracts (two-level: group → entry).

/// <summary>One FAQ group row in the admin grid (with its live entry count).</summary>
public sealed record AdminFaqGroupSummary(
    Guid Id,
    string NameEn,
    string NameAr,
    int DisplayOrder,
    bool IsActive,
    int EntryCount,
    DateTime CreatedAt);

public sealed class CreateFaqGroupRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public sealed class UpdateFaqGroupRequest
{
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>One FAQ question/answer entry (carries the full answer text so the
/// edit form can prefill without a second fetch — answers are bounded).</summary>
public sealed record AdminFaqEntrySummary(
    Guid Id,
    Guid FaqGroupId,
    string Question,
    string QuestionArabic,
    string Answer,
    string AnswerArabic,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt);

public sealed class CreateFaqEntryRequest
{
    public Guid FaqGroupId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string QuestionArabic { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string AnswerArabic { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public sealed class UpdateFaqEntryRequest
{
    public string Question { get; set; } = string.Empty;
    public string QuestionArabic { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string AnswerArabic { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
