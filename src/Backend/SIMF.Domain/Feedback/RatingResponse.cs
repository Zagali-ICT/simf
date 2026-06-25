using SIMF.Domain.Common;

namespace SIMF.Domain.Feedback;

/// <summary>
/// One attendee's submission against a <see cref="RatingType"/> (header). Carries
/// the optional overall star score, the optional end comment, and owns the
/// per-question <see cref="RatingAnswer"/> rows. A unique index on
/// <c>(UserId, RatingTypeId, TargetId)</c> enforces one submission per user per
/// target; the submit service upserts onto it (mirrors the old <c>Rating</c>
/// upsert, generalised to many types).
/// <para>
/// INVARIANT — responses are <b>upsert-only</b>: there is no soft-delete path. The
/// upsert reactivates an existing row in place rather than inserting a second one,
/// so the unbenefited <c>IsActive</c> flag (inherited from
/// <see cref="BaseAuditEntity"/>) is always <c>true</c> and the unique index is
/// safe. If a "withdraw my feedback" path is ever added it MUST hard-delete the row
/// (or the unique index must become a filtered <c>WHERE IsActive = 1</c> index),
/// otherwise a soft-deleted row would block the same user's next submission.
/// </para>
/// </summary>
public sealed class RatingResponse : BaseAuditEntity
{
    /// <summary>The attendee who submitted (<c>SimfUser.Id</c> / <c>sub</c>).
    /// Bare Guid — a logical cross-DB reference to Identity, never a DB FK
    /// (the Data ↔ Identity separation rule, D-157).</summary>
    public Guid UserId { get; set; }

    /// <summary>Owning rating type (real DB FK — same DbContext).</summary>
    public Guid RatingTypeId { get; set; }
    public RatingType? Type { get; set; }

    /// <summary>The rated entity for a per-session type (the <c>Session.Id</c>).
    /// For a <see cref="Common.Enums.RatingScope.Global"/> type this is
    /// <see cref="Guid.Empty"/> (a sentinel so the unique index stays uniform —
    /// SQL Server treats NULLs as distinct). No DB FK: the target is polymorphic
    /// and validated in the service.</summary>
    public Guid TargetId { get; set; }

    /// <summary>The overall 1–5 score when the type has an overall bar; null when
    /// the type collects per-question scores only.</summary>
    public int? OverallStars { get; set; }

    /// <summary>Optional free-text comment captured at the end (when the type
    /// allows it). Null when the attendee leaves it blank.</summary>
    public string? Comment { get; set; }

    /// <summary>The per-question scores.</summary>
    public ICollection<RatingAnswer> Answers { get; set; } = new List<RatingAnswer>();
}
