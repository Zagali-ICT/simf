namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// A7-20 (NCA) — stores and queries retired password hashes for reuse prevention.
/// </summary>
public interface IPasswordHistoryRepository
{
    /// <summary>The most recent <paramref name="take"/> retired password hashes for
    /// the account, newest first. Empty when <paramref name="take"/> &lt;= 0.</summary>
    Task<IReadOnlyList<string>> GetRecentHashesAsync(
        Guid userId, int take, CancellationToken cancellationToken = default);

    /// <summary>Records a retired password hash and prunes the account's history to
    /// the most recent <paramref name="keep"/> entries.</summary>
    Task RecordAsync(
        Guid userId, string passwordHash, int keep, CancellationToken cancellationToken = default);
}
