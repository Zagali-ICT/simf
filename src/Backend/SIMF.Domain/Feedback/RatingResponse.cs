using SIMF.Domain.Common;

namespace SIMF.Domain.Feedback;

/// <summary>One attendee's submission against a <see cref="RatingType"/>, owning its
/// per-question <see cref="RatingAnswer"/> rows. Upsert-only, never soft-deleted: an
/// inactive row would still block that user's next submission under the unique
/// (user, type, target) index.</summary>
public sealed class RatingResponse : BaseAuditEntity
{
    /// <summary>The attendee, who lives in the Identity database: a bare Guid, never a
    /// foreign key.</summary>
    public Guid UserId { get; set; }

    public Guid RatingTypeId { get; set; }
    public RatingType? Type { get; set; }

    /// <summary>The rated session; <see cref="Guid.Empty"/> on a global type. Not a
    /// foreign key — the target is polymorphic and the service validates it.</summary>
    public Guid TargetId { get; set; }

    public int? OverallStars { get; set; }

    public string? Comment { get; set; }

    public ICollection<RatingAnswer> Answers { get; set; } = new List<RatingAnswer>();
}
