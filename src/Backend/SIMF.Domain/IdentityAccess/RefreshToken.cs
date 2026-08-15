namespace SIMF.Domain.IdentityAccess;

/// <summary>Rotated on every use: presenting an already-revoked token is treated
/// as reuse. Timestamps are Saudi local time.</summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>The token this one replaced; null for the first in a chain.</summary>
    public Guid? RotatedFromId { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && now < ExpiresAt;
}
