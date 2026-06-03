using SIMF.Domain.Common;

namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A refresh token issued to a user. The token value itself is never stored —
/// only its hash. Tokens are rotated on use; presenting a token that has
/// already been revoked is detected as reuse. <see cref="RotatedFromId"/>
/// records the rotation chain for forensic reconstruction (SIMF-FDS-001
/// section 5.3, SIMF-DAT-001 section 5.1).
/// </summary>
public class RefreshToken: BaseEntity
{
    /// <summary>The hash of the token value; the value itself is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;
  
    /// <summary>
    /// The token this one replaced when it was rotated; null for the first
    /// token in a chain. Kept so a rotation chain can be reconstructed.
    /// </summary>
    public Guid? RotatedFromId { get; set; }




    /// <summary>True when the token is neither revoked nor expired at <paramref name="now"/>.</summary>
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;



    /// <summary>When the token expires (UTC).</summary>
    public DateTimeOffset ExpiresAt { get; set; }


    /// <summary>When the token was revoked (UTC); null while it is live.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

}
