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

    public async Task UpdateAsync(SecondFactorToken token, CancellationToken cancellationToken = default)
    {
        dbContext.SecondFactorTokens.Update(token);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
