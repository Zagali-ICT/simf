using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Domain.IdentityAccess;

using SIMF.Common.Enums;

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
        DateTime since,
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

    public async Task<bool> TryConsumeAsync(
        Guid codeId, DateTime now, CancellationToken cancellationToken = default)
    {
        var affected = await dbContext.AccountCodes
            .Where(code => code.Id == codeId && code.ConsumedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(code => code.ConsumedAt, now),
                cancellationToken);
        return affected == 1;
    }

    public async Task<int> IncrementAttemptCountAsync(
        Guid codeId, CancellationToken cancellationToken = default)
    {
        await dbContext.AccountCodes
            .Where(code => code.Id == codeId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    code => code.AttemptCount, code => code.AttemptCount + 1),
                cancellationToken);
        return await dbContext.AccountCodes
            .AsNoTracking()
            .Where(code => code.Id == codeId)
            .Select(code => code.AttemptCount)
            .SingleAsync(cancellationToken);
    }
}
