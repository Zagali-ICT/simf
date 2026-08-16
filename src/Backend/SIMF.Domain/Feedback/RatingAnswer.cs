namespace SIMF.Domain.Feedback;

/// <summary>One per-question score inside a <see cref="RatingResponse"/>. It carries no
/// audit stamps of its own: the owning response carries them.</summary>
public sealed class RatingAnswer
{
    public Guid Id { get; set; }

    public Guid RatingResponseId { get; set; }
    public RatingResponse? Response { get; set; }

    public Guid RatingQuestionId { get; set; }
    public RatingQuestion? Question { get; set; }

    public int Stars { get; set; }
}
