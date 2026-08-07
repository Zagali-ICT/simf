using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Repositories;

internal sealed class SecondFactorTokenRepository(SimfIdentityDbContext dbContext)
    : ISecondFactorTokenRepository
{
    public async Task AddAsync(SecondFactorToken token, CancellationToken cancellationToken = default)
    {
        dbContext.SecondFactorTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<SecondFactorToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        dbContext.SecondFactorTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task<bool> TryConsumeAsync(
        Guid tokenId, DateTime now, CancellationToken cancellationToken = default)
    {
        // Single conditional UPDATE: only the row that is still unconsumed is
        // touched, so exactly one concurrent caller sees affected == 1.
        var affected = await dbContext.SecondFactorTokens
            .Where(token => token.Id == tokenId && token.ConsumedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.ConsumedAt, now),
                cancellationToken);
        return affected == 1;
    }

    public Task IncrementAttemptCountAsync(
        Guid tokenId, CancellationToken cancellationToken = default) =>
        dbContext.SecondFactorTokens
            .Where(token => token.Id == tokenId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    token => token.AttemptCount, token => token.AttemptCount + 1),
                cancellationToken);

    public async Task UpdateAsync(SecondFactorToken token, CancellationToken cancellationToken = default)
    {
        dbContext.SecondFactorTokens.Update(token);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
