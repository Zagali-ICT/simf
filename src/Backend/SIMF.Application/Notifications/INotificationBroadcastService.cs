using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.Notifications;

/// <summary>
/// The Control Panel "Announcements" desk — composes a manual admin notification
/// broadcast, persists it as a Pending job, and (via the background
/// <c>NotificationBroadcastWorker</c>) fans it out as one in-app notification row +
/// one queued email per recipient. Recipients are either a session's active
/// seat-holders or a broad audience; they are resolved at send time across the
/// Identity/App DB boundary and are never stored on the job.
/// </summary>
public interface INotificationBroadcastService
{
    /// <summary>Validate + persist a Pending broadcast. Returns the job id + the
    /// recipient estimate at submit time (the worker computes the final count).</summary>
    Task<AdminBroadcastResult> CreateAsync(
        Guid actorUserId, AdminCreateBroadcastRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>The distinct-recipient count a target would reach right now — powers
    /// the composer's live recipient count.</summary>
    Task<AdminBroadcastEstimateResult> EstimateAsync(
        AdminBroadcastEstimateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Claim + fan out the single oldest Pending broadcast. Returns true if
    /// one was processed, false if none were pending. Called by the worker.</summary>
    Task<bool> ProcessNextPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Marks broadcasts left in Processing by an interrupted run (a crash
    /// mid-send) as Failed so they surface in history instead of hanging. Called
    /// once at worker startup. Returns the number recovered.</summary>
    Task<int> RecoverStalledAsync(CancellationToken cancellationToken = default);

    /// <summary>The Announcements history grid (newest first).</summary>
    Task<GridPage<AdminBroadcastSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>One broadcast's status detail; null when not found.</summary>
    Task<AdminBroadcastSummary?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);
}
