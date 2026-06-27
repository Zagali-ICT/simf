namespace SIMF.Domain.Programme;

/// <summary>
/// A user's "favourite" (المفضلة) mark on a programme <see cref="Session"/> —
/// the heart toggle on the session-summaries (1388:8392) and my-sessions
/// (1388:9067) screens. One row per (user, session); un-favouriting deletes the
/// row. <see cref="UserId"/> is a bare Guid (logical FK to <c>SimfUser.Id</c> in
/// the Identity DB — no cross-DB FK, D-157); <see cref="SessionId"/> is a real
/// FK to <see cref="Session"/> (same App DB).
/// </summary>
public sealed class SessionFavourite
{
    public Guid Id { get; set; }

    /// <summary>The signed-in user who favourited (bare Guid — no DB FK).</summary>
    public Guid UserId { get; set; }

    /// <summary>The favourited session (real FK, same DbContext).</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>When the favourite was added (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
