namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A single-use code a user can submit in place of an authenticator app's TOTP
/// code once they have lost access to their device. Issued in batches at
/// enrolment and on regeneration, and hashed at rest with the same scheme the
/// refresh and second-factor tokens use.
/// </summary>
public sealed class TotpRecoveryCode
{
    public Guid Id { get; set; }

    /// <summary>The account that owns this code.</summary>
    public Guid UserId { get; set; }

    /// <summary>A hash of the code. The plaintext is never persisted.</summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>Saudi local time.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the code was consumed; null while it is still usable.</summary>
    public DateTime? ConsumedAt { get; set; }
}
