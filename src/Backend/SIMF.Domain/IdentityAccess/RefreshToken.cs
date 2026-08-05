namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A refresh token issued to a user. The token value itself is never stored —
/// only its hash. Tokens are rotated on use; presenting a token that has
/// already been revoked is detected as reuse. <see cref="RotatedFromId"/>
/// records the rotation chain, so one can be reconstructed after the fact.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    /// <summary>The user the token belongs to.</summary>
    public Guid UserId { get; set; }

    /// <summary>The hash of the token value; the value itself is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>When the token expires (Saudi local).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>When the token was created (Saudi local).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the token was revoked (Saudi local); null while it is live.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// The token this one replaced when it was rotated; null for the first
    /// token in a chain. Kept so a rotation chain can be reconstructed.
    /// </summary>
    public Guid? RotatedFromId { get; set; }

    /// <summary>True when the token is neither revoked nor expired at <paramref name="now"/>.</summary>
    public bool IsActive(DateTime now) => RevokedAt is null && now < ExpiresAt;
}
