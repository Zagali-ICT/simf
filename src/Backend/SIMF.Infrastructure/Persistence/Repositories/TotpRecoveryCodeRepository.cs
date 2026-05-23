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

    public Task<TotpRecoveryCode?> FindActiveAsync(
        Guid userId,
        string codeHash,
        CancellationToken cancellationToken = default) =>
        dbContext.TotpRecoveryCodes.SingleOrDefaultAsync(
            code => code.UserId == userId
                && code.CodeHash == codeHash
                && code.ConsumedAt == null,
            cancellationToken);

    public async Task ConsumeAsync(
        TotpRecoveryCode code,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        code.ConsumedAt = now;
        dbContext.TotpRecoveryCodes.Update(code);
        await dbContext.SaveChangesAsync(cancellationToken);
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
