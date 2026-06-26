namespace SIMF.Contracts.Faq;

// Public, app-facing FAQ read contract (the accordion on the app's الأسئلة
// الشائعة screen, Figma 1388:7567). Read-only projection of the active groups
// and their active entries — no audit / IsActive / DisplayOrder leak to the
// public wire (the server already orders + filters).

/// <summary>One question/answer pair, bilingual.</summary>
public sealed record PublicFaqEntry(
    Guid Id,
    string Question,
    string QuestionArabic,
    string Answer,
    string AnswerArabic);

/// <summary>One FAQ group with its ordered, active entries.</summary>
public sealed record PublicFaqGroup(
    Guid Id,
    string Name,
    string NameArabic,
    IReadOnlyList<PublicFaqEntry> Entries);
