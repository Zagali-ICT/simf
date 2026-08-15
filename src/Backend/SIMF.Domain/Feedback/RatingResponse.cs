using SIMF.Domain.Common;

namespace SIMF.Domain.Feedback;

/// <summary>
/// One attendee's submission against a <see cref="RatingType"/>, carrying the
/// optional overall score and closing comment and owning the per-question
/// <see cref="RatingAnswer"/> rows. A unique index over the user, type and target
/// enforces one submission each, and the submit service upserts onto it.
///
/// <para>Responses are upsert-only, with no soft-delete path: the upsert
/// reactivates an existing row in place rather than inserting a second, so the
/// inherited active flag is always true and the unique index stays safe. Adding a
/// "withdraw my feedback" path would have to hard-delete the row, or make the
/// index filtered on the active flag — otherwise a soft-deleted row would block
/// that user's next submission.</para>
/// </summary>
public sealed class RatingResponse : BaseAuditEntity
{
    /// <summary>The attendee who submitted. A bare Guid: the user lives in the
    /// Identity database, so there is no foreign key across the two.</summary>
    public Guid UserId { get; set; }

    /// <summary>A real foreign key, since the type lives in the same
    /// database.</summary>
    public Guid RatingTypeId { get; set; }
    public RatingType? Type { get; set; }

    /// <summary>The rated entity for a per-session type. A global type stores
    /// <see cref="Guid.Empty"/> rather than null, which keeps the column
    /// non-nullable and every row in the unique index over (user, type, target)
    /// carrying a value. Nullability is not what makes that index work: SQL Server
    /// treats nulls as EQUAL inside a unique index, so a nullable target would
    /// have enforced once-per-user on a global type just as well. No foreign key:
    /// the target is polymorphic and the service validates it.</summary>
    public Guid TargetId { get; set; }

    /// <summary>The overall score when the type shows an overall bar; null when
    /// it collects per-question scores only.</summary>
    public int? OverallStars { get; set; }

    /// <summary>The closing comment, when the type allows one and the attendee
    /// wrote it.</summary>
    public string? Comment { get; set; }

    public ICollection<RatingAnswer> Answers { get; set; } = new List<RatingAnswer>();
}
