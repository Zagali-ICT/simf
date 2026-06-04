using SIMF.Domain.Common;

namespace SIMF.Domain.Feedback;

/// <summary>
/// D-199 (Mockup screen 40 "Rate the Forum") — a single overall forum
/// rating per attendee. Per the owner default each user has at most one
/// rating: the unique index on <see cref="UserId"/> enforces
/// "one rating per user", and the rate endpoint upserts onto it.
/// <para>
/// This entity deliberately does not derive from
/// <c>SIMF.Domain.Common.BaseEntity</c>: it follows the lean per-attendee
/// shape used by <c>SeatReservation</c> (no <c>CreateBy</c>/<c>UpdateBy</c>
/// columns — the owning user is <see cref="UserId"/>), keeping the wire
/// and table contract minimal for the high-volume feedback path.
/// </para>
/// </summary>
public sealed class Rating:BaseAuditEntity
{ 

    /// <summary>The attendee who submitted the rating
    /// (<c>SimfUser.Id</c> / <c>sub</c> claim). Unique — one row per user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Overall experience score, 1–5 inclusive. Validated at the
    /// edge (FluentValidation) and re-checked in the service.</summary>
    public int Stars { get; set; }

    /// <summary>Optional free-text comment ("Comments (optional)" on the
    /// mockup). Null when the attendee submits stars only.</summary>
    public string? Comment { get; set; }

    

    /// <summary>When the rating was last changed via upsert; null until the
    /// attendee revises their original submission.</summary>
    //public DateTimeOffset? UpdatedAt { get; set; }

     
}
