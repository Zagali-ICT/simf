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

    public Task<AccountCode?> GetLatestUnconsumedAsync(
        Guid userId,
        AccountCodePurpose purpose,
        CancellationToken cancellationToken = default) =>
        dbContext.AccountCodes
            .Where(code => code.UserId == userId
                && code.Purpose == purpose
                && code.ConsumedAt == null)
            .OrderByDescending(code => code.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountCreatedSinceAsync(
        Guid userId,
        AccountCodePurpose purpose,
        DateTimeOffset since,
        CancellationToken cancellationToken = default) =>
        dbContext.AccountCodes.CountAsync(
            code => code.UserId == userId
                && code.Purpose == purpose
                && code.CreatedAt >= since,
            cancellationToken);

    public async Task UpdateAsync(AccountCode code, CancellationToken cancellationToken = default)
    {
        dbContext.AccountCodes.Update(code);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
