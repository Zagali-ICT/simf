namespace SIMF.Domain.SessionComments;

/// <summary>
/// B5 — D-223: one audience member's "like" on a <see cref="SessionComment"/>
/// (Mockup page 28). A per-user join with a COMPOSITE primary key
/// <c>(CommentId, UserId)</c> — the key itself is the per-user-once
/// uniqueness AND the idempotency guard (a second like by the same user
/// collides on the PK). Mirrors <c>SessionModerator</c>'s keyless composite
/// shape. The denormalised <see cref="SessionComment.LikeCount"/> is kept in
/// step inside the like/unlike service path.
/// </summary>
public sealed class SessionCommentLike
{
    /// <summary>Real FK to <see cref="SessionComment"/> on the App DB.</summary>
    public Guid CommentId { get; set; }
    public SessionComment? Comment { get; set; }

    /// <summary>Logical FK to <c>SimfUser.Id</c> on the Identity DB — no SQL
    /// constraint (cross-DB), like <see cref="SessionComment.UserId"/>.</summary>
    public Guid UserId { get; set; }

    /// <summary>When the like was recorded (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
