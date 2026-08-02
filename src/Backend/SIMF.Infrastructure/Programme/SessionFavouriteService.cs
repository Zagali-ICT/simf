// Tests: SIMF.Api.Tests/SessionFavouriteTests.cs
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

        dbContext.SessionFavourites.Add(new SessionFavourite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionId = sessionId,
            CreatedAt = timeProvider.SimfNow(),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
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
}
