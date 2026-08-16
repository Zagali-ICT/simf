namespace SIMF.Domain.IdentityAccess;

/// <summary>A7-20 (NCA Secure Application-Development Standard): a retired
/// password hash, so a change or reset can refuse a recent password. Append-only,
/// pruned to the configured depth. CreatedAt is Saudi local time.</summary>
public class PasswordHistoryEntry
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
