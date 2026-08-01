using SIMF.Common;
using SIMF.Common.Enums;

namespace SIMF.Contracts.Feedback;

// Admin contracts for the dynamic rating system: CRUD over types → groups →
// questions, the read-only responses viewer, and the KPI aggregates.

// ── Rating types ─────────────────────────────────────────────────────────────

/// <summary>One rating type in the admin grid (with live child counts).</summary>
public sealed record AdminRatingTypeSummary(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    RatingScope Scope,
    bool HasOverallStars,
    bool AllowComment,
    string? CommentLabel,
    string? CommentLabelArabic,
    bool IsSystem,
    int DisplayOrder,
    bool IsActive,
    int GroupCount,
    int QuestionCount,
    int ResponseCount,
    DateTime CreatedAt);

public sealed class CreateRatingTypeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public RatingScope Scope { get; set; } = RatingScope.Global;
    public bool HasOverallStars { get; set; } = true;
    public bool AllowComment { get; set; } = true;
    public string? CommentLabel { get; set; }
    public string? CommentLabelArabic { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class UpdateRatingTypeRequest
{
    // Code + Scope are immutable after create for system types and stable for
    // custom types; only the editable surface is here.
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public bool HasOverallStars { get; set; } = true;
    public bool AllowComment { get; set; } = true;
    public string? CommentLabel { get; set; }
    public string? CommentLabelArabic { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

// ── Question groups ──────────────────────────────────────────────────────────

public sealed record AdminRatingQuestionGroupSummary(
    Guid Id,
    Guid RatingTypeId,
    string Name,
    string NameArabic,
    int DisplayOrder,
    bool IsActive,
    int QuestionCount,
    DateTime CreatedAt);

public sealed class CreateRatingQuestionGroupRequest
{
    public Guid RatingTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public sealed class UpdateRatingQuestionGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

// ── Questions ────────────────────────────────────────────────────────────────

public sealed record AdminRatingQuestionSummary(
    Guid Id,
    Guid RatingTypeId,
    Guid? RatingQuestionGroupId,
    string Text,
    string TextArabic,
    bool IsRequired,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt);

public sealed class CreateRatingQuestionRequest
{
    public Guid RatingTypeId { get; set; }
    public Guid? RatingQuestionGroupId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string TextArabic { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class UpdateRatingQuestionRequest
{
    public Guid? RatingQuestionGroupId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string TextArabic { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

// ── Responses viewer + KPI ───────────────────────────────────────────────────

/// <summary>One submitted response in the admin grid.</summary>
public sealed record AdminRatingResponseSummary(
    Guid Id,
    Guid RatingTypeId,
    string RatingTypeName,
    string RatingTypeCode,
    Guid UserId,
    Guid TargetId,
    int? OverallStars,
    string? Comment,
    int AnswerCount,
    double? AverageAnswerStars,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>The admin responses page: the grid + the headline aggregates over
/// the filtered active set.</summary>
public sealed record AdminRatingResponsesPage(
    GridPage<AdminRatingResponseSummary> Responses,
    double AverageOverall,
    int ResponseCount);

/// <summary>Per-question KPI (count + average) within a type.</summary>
public sealed record RatingQuestionKpi(
    Guid QuestionId,
    string Text,
    string TextArabic,
    int Count,
    double Average);

/// <summary>Per-type KPI: response count, overall average, per-question averages.</summary>
public sealed record RatingTypeKpi(
    Guid RatingTypeId,
    string Code,
    string Name,
    string NameArabic,
    int ResponseCount,
    double AverageOverall,
    IReadOnlyList<RatingQuestionKpi> Questions);

/// <summary>The whole rating-KPI dashboard payload.</summary>
public sealed record AdminRatingKpiView(
    int TotalResponses,
    IReadOnlyList<RatingTypeKpi> Types);
