using SIMF.Contracts.Networking;

namespace SIMF.Application.Networking.Abstractions;

/// <summary>
/// B6 — D-224: visitor-to-visitor networking connections. App-facing only
/// (used by the mobile networking screen): a caller requests a connection,
/// the target accepts, and either party can remove it. There is no admin/CP
/// surface, so the endpoints carry no permission code (RequireApprovedAccount
/// only), mirroring SessionComment submit / MeetPeopleLikeYou.
/// </summary>
public interface INetworkingService
{
    /// <summary>Request a connection with another user. Rejects self-connect,
    /// an unknown target, and a duplicate active connection in either
    /// direction. Lands <c>Pending</c>.</summary>
    Task<ConnectionResult> RequestAsync(
        Guid requesterUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Accept a pending connection. Only the target may accept;
    /// re-accepting an already-accepted connection is an idempotent no-op.</summary>
    Task<ConnectionResult> AcceptAsync(
        Guid actorUserId,
        Guid connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>Remove / decline a connection (soft-delete). Either party may
    /// act; removing an already-removed connection is idempotent.</summary>
    Task RemoveAsync(
        Guid actorUserId,
        Guid connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>The caller's active connections (pending + accepted), with the
    /// other party's display name and the request direction.</summary>
    Task<IReadOnlyList<ConnectionRow>> ListMineAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
