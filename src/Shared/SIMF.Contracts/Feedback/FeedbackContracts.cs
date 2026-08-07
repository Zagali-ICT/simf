using SIMF.Common.Enums;

namespace SIMF.Contracts.Feedback;

// App-facing contracts for the dynamic, config-driven rating system. The app
// fetches the form for a rating type (+ optional target), renders the overall
// star bar / grouped + flat questions / comment box per the type's config, then
// posts the submission. Replaces the old fixed RateRequest/RatingView.

/// <summary>Query for the app rating form. Resolve the type by <see cref="Code"/>
/// (e.g. "App"/"Session") or by <see cref="RatingTypeId"/>; <see cref="TargetId"/>
/// is the rated entity (a Session id) for a per-session type.</summary>
public sealed class RatingFormRequest
{
    public string? Code { get; set; }
    public Guid? RatingTypeId { get; set; }
    public Guid? TargetId { get; set; }
}

/// <summary>One question on the rating form — a fixed 1–5 star scale.</summary>
public sealed record RatingFormQuestion(
    Guid Id,
    string Text,
    string TextArabic,
    bool IsRequired,
    int DisplayOrder);

/// <summary>A group of questions on the form (Speaker / Sound / Light …).</summary>
public sealed record RatingFormGroup(
    Guid Id,
    string Name,
    string NameArabic,
    int DisplayOrder,
    IReadOnlyList<RatingFormQuestion> Questions);

/// <summary>One prefilled answer when the user already submitted this form.</summary>
public sealed record RatingExistingAnswer(Guid QuestionId, int Stars);

/// <summary>The user's existing submission for this type/target, for prefill.</summary>
public sealed record RatingExistingSubmission(
    int? OverallStars,
    string? Comment,
    IReadOnlyList<RatingExistingAnswer> Answers);

/// <summary>The full rating form the app renders: the type's config + its
/// grouped and ungrouped questions + the user's existing submission (if any).</summary>
public sealed record RatingFormView(
    Guid RatingTypeId,
    string Code,
    string Name,
    string NameArabic,
    RatingScope Scope,
    bool HasOverallStars,
    bool AllowComment,
    string? CommentLabel,
    string? CommentLabelArabic,
    Guid? TargetId,
    IReadOnlyList<RatingFormGroup> Groups,
    IReadOnlyList<RatingFormQuestion> UngroupedQuestions,
    RatingExistingSubmission? Existing,
    // The rated target's display context, for the app's
    // "watched at {session} · {date}" header on a per-session rating. Populated
    // for a per-session target; null for a Global type. Appended (the wire
    // contract is append-only) so the shipped app decodes unchanged.
    string? TargetName = null,
    string? TargetNameArabic = null,
    DateTime? TargetStart = null,
    // False when the caller has not attended what this type rates,
    // so the app disables submit and shows an "attend to rate" note. The hard gate is
    // still on POST /feedback/submit (403 RATING_NOT_ATTENDED). Appended (the wire
    // contract is append-only); defaults true so the shipped app decodes unchanged.
    bool IsEligible = true);

/// <summary>One per-question score in a submission.</summary>
public sealed record RatingAnswerInput(Guid QuestionId, int Stars);

/// <summary>The attendee's submission. Resolve the type by <see cref="RatingTypeId"/>
/// or <see cref="Code"/>; <see cref="TargetId"/> is required for a per-session type.
/// <see cref="OverallStars"/> is required when the type shows the overall bar.</summary>
public sealed class SubmitRatingRequest
{
    public Guid RatingTypeId { get; set; }
    public string? Code { get; set; }
    public Guid? TargetId { get; set; }
    public int? OverallStars { get; set; }
    public string? Comment { get; set; }
    public List<RatingAnswerInput> Answers { get; set; } = new();
}

/// <summary>The saved submission echoed back to the app.</summary>
public sealed record RatingSubmissionView(
    Guid Id,
    Guid RatingTypeId,
    Guid? TargetId,
    int? OverallStars,
    string? Comment,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<RatingAnswerInput> Answers);
