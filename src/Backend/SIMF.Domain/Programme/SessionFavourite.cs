namespace SIMF.Domain.Programme;

/// <summary>A user's favourite mark on a <see cref="Session"/>; un-favouriting
/// deletes the row rather than deactivating it.</summary>
public sealed class SessionFavourite
{
    public Guid Id { get; set; }

    /// <summary>Bare Guid: the user lives in the Identity database, so no key crosses the two.</summary>
    public Guid UserId { get; set; }

    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime CreatedAt { get; set; }
}
