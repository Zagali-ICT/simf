namespace SIMF.Domain.Programme;

/// <summary>
/// A user's favourite mark on a <see cref="Session"/>, behind the heart toggle
/// on the session and my-sessions screens. One row per user and session;
/// un-favouriting deletes it outright rather than deactivating it.
/// </summary>
public sealed class SessionFavourite
{
    public Guid Id { get; set; }

    /// <summary>A bare Guid: the user lives in the Identity database, so there is
    /// no foreign key across the two.</summary>
    public Guid UserId { get; set; }

    /// <summary>A real foreign key, since the session lives in the same
    /// database.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime CreatedAt { get; set; }
}
