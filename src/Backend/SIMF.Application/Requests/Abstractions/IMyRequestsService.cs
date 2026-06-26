using SIMF.Contracts.Requests;

namespace SIMF.Application.Requests.Abstractions;

/// <summary>
/// D-500 (Wave 5, الطلبات 1408:9726) — the unified "My requests" feed for the
/// mobile app: every request the signed-in user submitted, newest first —
/// speaker meetings (D-269), delegation meetings (D-478, read-only),
/// session-attendance seat bookings (D-175/D-227, surfaced), and the two new
/// standalone types (participation-document + badge-update, D-500). Supersedes
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
