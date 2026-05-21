using Microsoft.EntityFrameworkCore;
using SIMF.Application.Abstractions;

namespace SIMF.Infrastructure.Persistence;

/// <summary>
/// Runs a unit of work inside a <see cref="SimfIdentityDbContext"/> transaction.
/// It uses the EF Core execution strategy, so the transaction composes
/// correctly with <c>EnableRetryOnFailure</c> — a manual transaction under a
/// retrying strategy throws otherwise.
/// </summary>
internal sealed class TransactionRunner(SimfIdentityDbContext dbContext) : ITransactionRunner
{
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
