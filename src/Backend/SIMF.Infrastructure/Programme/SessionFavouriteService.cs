// Tests: SIMF.Api.Tests/SessionFavouriteTests.cs
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>Session favourites (المفضلة) over <see cref="SimfAppDbContext"/>.
/// Add/remove are idempotent; the list is the caller's favourited session ids.
/// A favourite on an unknown/inactive session is a 404.</summary>
internal sealed class SessionFavouriteService(
    SimfAppDbContext dbContext,
    TimeProvider timeProvider) : ISessionFavouriteService
{
    public async Task AddAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var sessionExists = await dbContext.Sessions
            .AnyAsync(s => s.Id == sessionId && s.IsActive, cancellationToken);
        if (!sessionExists)
        {
            throw new ApiException(ErrorCodes.NotFound, 404,
                "The session was not found.", "لم يتم العثور على الجلسة.");
        }

        var already = await dbContext.SessionFavourites
            .AnyAsync(f => f.UserId == userId && f.SessionId == sessionId, cancellationToken);
        if (already) { return; } // idempotent

        var row = new SessionFavourite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionId = sessionId,
            CreatedAt = timeProvider.SimfNow(),
        };
        dbContext.SessionFavourites.Add(row);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
        {
            // The check above and this insert are not one atomic unit, so a
            // double-tapped heart (or an app retry of a request whose response was
            // lost) can put two inserts either side of it. The unique index
            // (UserId, SessionId) rejects the loser — which means the favourite
            // already exists, i.e. exactly the outcome this idempotent operation
            // promises. Drop the rejected row and return success instead of
            // surfacing a 500 for work that in fact succeeded.
            dbContext.Entry(row).State = EntityState.Detached;
        }
    }

    public async Task RemoveAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.SessionFavourites
            .SingleOrDefaultAsync(
                f => f.UserId == userId && f.SessionId == sessionId, cancellationToken);
        if (row is null) { return; } // idempotent

        dbContext.SessionFavourites.Remove(row);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListMineAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.SessionFavourites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => f.SessionId)
            .ToListAsync(cancellationToken);

    /// <summary>True only when the store rejected the write on a UNIQUE index —
    /// SQL Server 2601 (duplicate key in a unique index) or 2627 (unique
    /// constraint). Every other failure EF wraps in a
    /// <see cref="DbUpdateException"/> must propagate; swallowing them would hide
    /// a real write failure behind a silent success.</summary>
    private static bool IsUniqueIndexViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
