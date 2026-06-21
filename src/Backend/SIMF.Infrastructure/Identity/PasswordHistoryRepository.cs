using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>A7-20 (NCA) — EF-backed retired-password-hash store (Identity DB).</summary>
internal sealed class PasswordHistoryRepository(
    SimfIdentityDbContext dbContext,
    TimeProvider timeProvider) : IPasswordHistoryRepository
{
    public async Task<IReadOnlyList<string>> GetRecentHashesAsync(
        Guid userId, int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }
        return await dbContext.PasswordHistory
            .Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Select(entry => entry.PasswordHash)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task RecordAsync(
        Guid userId, string passwordHash, int keep, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.PasswordHistory
            .Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        dbContext.PasswordHistory.Add(new PasswordHistoryEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PasswordHash = passwordHash,
            CreatedAtUtc = timeProvider.GetUtcNow(),
        });

        // Keep the new entry plus the (keep - 1) most recent existing ones.
        var stale = existing.Skip(Math.Max(0, keep - 1)).ToList();
        if (stale.Count > 0)
        {
            dbContext.PasswordHistory.RemoveRange(stale);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
