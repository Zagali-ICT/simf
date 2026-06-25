namespace SIMF.Domain.Feedback;

/// <summary>
/// One per-question score inside a <see cref="RatingResponse"/> (line). Lean
/// entity (no audit stamps — the owning response carries those); cascade-deleted
/// with its response. <c>(RatingResponseId, RatingQuestionId)</c> is unique, so a
/// question is scored at most once per submission.
/// </summary>
public sealed class RatingAnswer
{
    public Guid Id { get; set; }

    /// <summary>Owning response (real DB FK — same DbContext, cascade delete).</summary>
    public Guid RatingResponseId { get; set; }
    public RatingResponse? Response { get; set; }

    /// <summary>The question this score answers (real DB FK — same DbContext).</summary>
    public Guid RatingQuestionId { get; set; }
    public RatingQuestion? Question { get; set; }

    /// <summary>The fixed 1–5 score.</summary>
    public int Stars { get; set; }
}
