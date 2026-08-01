namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A single-use recovery code that a user can submit in place of an
/// authenticator-app TOTP code when they've lost access to their device
/// (decision D-040). Issued in batches of ten by
/// <see cref="SIMF.Application.IdentityAccess.IRecoveryCodeService"/>
/// at TOTP-enrolment time and on regeneration; hashed at rest with the same
/// SHA-256 scheme as <c>RefreshToken</c> and <c>SecondFactorToken</c>.
/// </summary>
public sealed class TotpRecoveryCode
{
    public Guid Id { get; set; }

    /// <summary>The account that owns this code.</summary>
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the plaintext code. The plaintext is never persisted.</summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>When the code was minted (Saudi local).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the code was consumed; null while it is still active.</summary>
    public DateTime? ConsumedAt { get; set; }
}
