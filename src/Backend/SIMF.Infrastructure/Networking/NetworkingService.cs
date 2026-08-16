// Tests: SIMF.Api.Tests/NetworkingTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Networking.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Networking;
using SIMF.Domain.Auditing;
using SIMF.Domain.Networking;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Networking;

/// <summary>
/// Visitor-to-visitor networking connections over
/// <see cref="SimfAppDbContext"/>. Display names are resolved from the
/// Identity DB in a separate round-trip (no cross-DB JOIN). In-service
/// validation throws <see cref="ApiException"/> with bilingual messages
/// (matches the MeetingRequest app-facing services); one
/// audit row per mutation.
/// </summary>
internal sealed class NetworkingService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<NetworkingService> logger) : INetworkingService
{
    public async Task<ConnectionResult> RequestAsync(
        Guid requesterUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (requesterUserId == targetUserId)
        {
            throw new ApiException(
                ErrorCodes.ConnectionSelf, 400,
                "You cannot connect with yourself.",
                "لا يمكنك الاتصال بنفسك.");
        }

        var targetExists = await identityDbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == targetUserId, cancellationToken);
        if (!targetExists)
        {
            throw new ApiException(
                ErrorCodes.ConnectionTargetNotFound, 404,
                "The user you tried to connect with was not found.",
                "لم يتم العثور على المستخدم الذي تحاول الاتصال به.");
        }

        // Reject a duplicate ACTIVE connection in either direction. Removed /
        // declined rows (IsActive=false) do not block a fresh request.
        var duplicate = await appDbContext.Connections
            .AsNoTracking()
            .AnyAsync(c => c.IsActive
                && ((c.RequesterUserId == requesterUserId && c.TargetUserId == targetUserId)
                    || (c.RequesterUserId == targetUserId && c.TargetUserId == requesterUserId)),
                cancellationToken);
        if (duplicate)
        {
            throw DuplicateConnection();
        }

        // The sorted pair is what IX_Connections_PairLowUserId_PairHighUserId keys on, so
        // it has to be written on the way in: sorted, a request and its mirror image
        // collapse to one key, and the index rejects the second of two concurrent
        // requests that both read past the duplicate check above.
        var pairLow = requesterUserId.CompareTo(targetUserId) < 0
            ? requesterUserId
            : targetUserId;
        var pairHigh = pairLow == requesterUserId ? targetUserId : requesterUserId;

        var now = timeProvider.SimfNow();
        var connection = new Connection
        {
            Id = Guid.NewGuid(),
            RequesterUserId = requesterUserId,
            TargetUserId = targetUserId,
            State = ConnectionState.Pending,
            PairLowUserId = pairLow,
            PairHighUserId = pairHigh,
            IsActive = true,
            CreatedAt = now,
        };
        appDbContext.Connections.Add(connection);
        try
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.ViolatesAnyIndex(
            "IX_Connections_PairLowUserId_PairHighUserId"))
        {
            // Lost the race to a concurrent request on the same pair, which is the same
            // situation the read above reports, so give the caller the same answer.
            throw DuplicateConnection();
        }

        await auditLog.WriteSuccessAsync(
            AuditEvents.ConnectionRequested,
            requesterUserId,
            $"connectionId={connection.Id}; targetUserId={targetUserId}",
            cancellationToken);

        logger.LogInformation(
            "Connection {ConnectionId} requested by {Requester} to {Target}",
            connection.Id, requesterUserId, targetUserId);

        return ToResult(connection);
    }

    public async Task<ConnectionResult> AcceptAsync(
        Guid actorUserId,
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var connection = await LoadActiveAsync(connectionId, cancellationToken);

        // Only the recipient may accept.
        if (connection.TargetUserId != actorUserId)
        {
            throw new ApiException(
                ErrorCodes.ConnectionInvalid, 403,
                "Only the recipient can accept this connection.",
                "يمكن للمستلِم فقط قبول هذا الاتصال.");
        }

        if (connection.State == ConnectionState.Accepted)
        {
            return ToResult(connection); // idempotent
        }

        connection.State = ConnectionState.Accepted;
        connection.RespondedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ConnectionAccepted,
            actorUserId,
            $"connectionId={connection.Id}",
            cancellationToken);

        return ToResult(connection);
    }

    public async Task RemoveAsync(
        Guid actorUserId,
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var connection = await appDbContext.Connections
            .SingleOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (connection is null || !connection.IsActive)
        {
            return; // idempotent — already gone
        }

        // Either party may remove / decline.
        if (connection.RequesterUserId != actorUserId
            && connection.TargetUserId != actorUserId)
        {
            throw new ApiException(
                ErrorCodes.ConnectionInvalid, 403,
                "You are not part of this connection.",
                "أنت لست طرفاً في هذا الاتصال.");
        }

        // The target turning down a request that is still Pending is a DECLINE, and it is
        // not the same act as either party dropping a connection that was already
        // accepted. Deactivate hides both from the feed, so the state is the only thing
        // left that tells the two apart afterwards.
        var declined = connection.TargetUserId == actorUserId
            && connection.State == ConnectionState.Pending;
        if (declined)
        {
            connection.State = ConnectionState.Declined;
        }

        connection.Deactivate();
        // First response only: RespondedAt is when the target answered, so a removal that
        // comes later must not drag it forward.
        connection.RespondedAt ??= timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ConnectionRemoved,
            actorUserId,
            declined
                ? $"connectionId={connection.Id}; declined=true"
                : $"connectionId={connection.Id}",
            cancellationToken);
    }

    public async Task<IReadOnlyList<ConnectionRow>> ListMineAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var rows = await appDbContext.Connections
            .AsNoTracking()
            .Where(c => c.IsActive
                && (c.RequesterUserId == actorUserId || c.TargetUserId == actorUserId))
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.RequesterUserId,
                c.TargetUserId,
                c.State,
                c.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<ConnectionRow>();
        }

        // The "other" party for each row — resolve their display names in one
        // Identity round-trip.
        var otherIds = rows
            .Select(r => r.RequesterUserId == actorUserId ? r.TargetUserId : r.RequesterUserId)
            .Distinct()
            .ToList();
        var names = await identityDbContext.Users
            .AsNoTracking()
            .Where(u => otherIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        return rows.Select(r =>
        {
            var otherId = r.RequesterUserId == actorUserId ? r.TargetUserId : r.RequesterUserId;
            names.TryGetValue(otherId, out var other);
            return new ConnectionRow(
                r.Id,
                otherId,
                other?.DisplayName ?? string.Empty,
                r.State,
                IsIncoming: r.TargetUserId == actorUserId,
                r.CreatedAt);
        }).ToList();
    }

    private async Task<Connection> LoadActiveAsync(
        Guid connectionId, CancellationToken cancellationToken)
    {
        var connection = await appDbContext.Connections
            .SingleOrDefaultAsync(c => c.Id == connectionId, cancellationToken);
        if (connection is null || !connection.IsActive)
        {
            throw new ApiException(
                ErrorCodes.ConnectionNotFound, 404,
                "The connection was not found.",
                "لم يتم العثور على الاتصال.");
        }
        return connection;
    }

    /// <summary>One bilingual 409 in one place, raised by both the duplicate read and the
    /// unique-index race that the read cannot cover.</summary>
    private static ApiException DuplicateConnection() =>
        new(ErrorCodes.ConnectionAlreadyExists, 409,
            "A connection with this user already exists.",
            "يوجد اتصال مع هذا المستخدم بالفعل.");

    private static ConnectionResult ToResult(Connection c) =>
        new(c.Id, c.RequesterUserId, c.TargetUserId, c.State, c.CreatedAt);
}
