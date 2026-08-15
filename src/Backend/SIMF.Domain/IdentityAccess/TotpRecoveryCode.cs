namespace SIMF.Domain.IdentityAccess;

/// <summary>Single-use stand-in for an authenticator TOTP code, issued in
/// batches at enrolment and on regeneration. Timestamps are Saudi local time.</summary>
public sealed class TotpRecoveryCode
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? ConsumedAt { get; set; }
}
