using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository(SimfIdentityDbContext dbContext) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<RefreshToken?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        dbContext.RefreshTokens.Update(token);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default) =>
        dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, revokedAt),
                cancellationToken);
}
