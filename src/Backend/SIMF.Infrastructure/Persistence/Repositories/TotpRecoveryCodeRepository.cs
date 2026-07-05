using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Repositories;

internal sealed class TotpRecoveryCodeRepository(SimfIdentityDbContext dbContext)
    : ITotpRecoveryCodeRepository
{
    public async Task AddBatchAsync(
        Guid userId,
        IReadOnlyList<string> codeHashes,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var entities = codeHashes.Select(hash => new TotpRecoveryCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = hash,
            CreatedAt = now,
        });
        dbContext.TotpRecoveryCodes.AddRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryConsumeAsync(
        Guid userId,
        string codeHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        // Single conditional UPDATE: the active code matching (user, hash) is
        // consumed in one atomic statement, so a concurrent double-submit of the
        // same code yields exactly one affected == 1.
        var affected = await dbContext.TotpRecoveryCodes
            .Where(code => code.UserId == userId
                && code.CodeHash == codeHash
                && code.ConsumedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(code => code.ConsumedAt, now),
                cancellationToken);
        return affected == 1;
    }

    public Task<int> CountActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        dbContext.TotpRecoveryCodes.CountAsync(
            code => code.UserId == userId && code.ConsumedAt == null,
            cancellationToken);

    public async Task RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.TotpRecoveryCodes
            .Where(code => code.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
