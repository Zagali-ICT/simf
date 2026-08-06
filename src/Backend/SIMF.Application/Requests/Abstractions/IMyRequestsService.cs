using SIMF.Contracts.Requests;

namespace SIMF.Application.Requests.Abstractions;

/// <summary>
/// The unified "My requests" (الطلبات) feed for the
/// mobile app: every request the signed-in user submitted, newest first —
/// speaker meetings, delegation meetings (read-only),
/// session-attendance seat bookings, and the two standalone
/// types (participation-document + badge-update). Supersedes
/// the old read-only <c>IMyMeetingsService</c>. Also owns the unified
/// self-cancel of a still-pending speaker / document / badge request.
/// </summary>
public interface IMyRequestsService
{
    Task<IReadOnlyList<AppRequestItem>> GetMyRequestsAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Cancels one of the user's own still-pending requests (speaker /
    /// document / badge). Throws 404 when not found / not owned, 409 when the
    /// kind is not self-cancellable or the request is no longer Pending.</summary>
    Task CancelAsync(
        Guid userId, AppRequestKind kind, Guid id,
        CancellationToken cancellationToken = default);
}
