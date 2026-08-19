using Microsoft.EntityFrameworkCore;
using SIMF.Application.Abstractions;

namespace SIMF.Infrastructure.Persistence;

/// <summary>
/// Runs a unit of work inside a <see cref="SimfIdentityDbContext"/> transaction.
/// It uses the EF Core execution strategy, so the transaction composes
/// correctly with <c>EnableRetryOnFailure</c> — a manual transaction under a
/// retrying strategy throws otherwise.
///
/// <para>Known limitation, deliberately not papered over here: a transient
/// failure raised BY the commit is ambiguous — the COMMIT may have reached the
/// server and succeeded before the connection dropped — and this overload simply
/// re-invokes the block, which is not idempotent for callers that create rows.
/// Closing that needs <c>ExecuteInTransactionAsync</c> with a caller-supplied
/// <c>verifySucceeded</c> predicate, which is a change to
/// <see cref="ITransactionRunner"/> and to every call site, not to this class
/// alone.</para>
/// </summary>
internal sealed class TransactionRunner(SimfIdentityDbContext dbContext) : ITransactionRunner
{
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        // The token goes to the strategy as well as into the block: without it the
        // strategy's own wait between retries is not cancellable, so a cancelled
        // request still sat out the back-off.
        await strategy.ExecuteAsync(
            async token =>
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(token);
                await action(token);
                await transaction.CommitAsync(token);
            },
            cancellationToken);
    }
}
