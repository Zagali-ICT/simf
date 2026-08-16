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

    /// <summary>The token this one replaced; null for the first in a chain.
    ///
    /// <para>Written on every rotation and read by nothing. Reuse is already caught
    /// without it: the rotation revokes the presented token conditionally, and a
    /// revoke that affects no row is what answers 401, so the chain adds no evidence
    /// the single row does not already carry. It is also deliberately NOT a declared
    /// self-reference — a replacement inherits the chain's original
    /// <see cref="ExpiresAt"/>, so the retention purge deletes a whole chain in one
    /// set-based statement, which a same-table foreign key can refuse outright.</para></summary>
    public Guid? RotatedFromId { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && now < ExpiresAt;
}
