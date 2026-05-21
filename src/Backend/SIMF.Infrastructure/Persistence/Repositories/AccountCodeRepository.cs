using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Repositories;

internal sealed class AccountCodeRepository(SimfIdentityDbContext dbContext) : IAccountCodeRepository
{
    public async Task AddAsync(AccountCode code, CancellationToken cancellationToken = default)
    {
        dbContext.AccountCodes.Add(code);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<AccountCode?> GetLatestActiveAsync(
        Guid userId,
        AccountCodePurpose purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        dbContext.AccountCodes
            .Where(code => code.UserId == userId
                && code.Purpose == purpose
                && code.ConsumedAt == null
                && code.ExpiresAt > now)
            .OrderByDescending(code => code.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task UpdateAsync(AccountCode code, CancellationToken cancellationToken = default)
    {
        dbContext.AccountCodes.Update(code);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
