using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>D-165 (gap doc G3) — admin CRUD over <c>Session</c>
/// (SIMF-FDS-004 §5.3 + PDF §2.9).</summary>
public interface IAdminSessionService
{
    Task<GridPage<AdminSessionSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminSessionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminSessionDetail> CreateAsync(
        Guid actorUserId, AdminCreateSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminSessionDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateSessionRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>P3.2 — D-231 (Completion Programme §5.2): the Scientific
    /// Committee moves the session along its broadcast lifecycle. Only the
    /// adjacent transitions are legal
    /// (<c>Scheduled ↔ Held ↔ Recorded ↔ Published</c>); an illegal jump
    /// throws <c>SESSION_STATUS_TRANSITION_INVALID</c> (400). Moving to
    /// <see cref="SIMF.Common.Enums.SessionStatus.Published"/> stamps
    /// <c>PublishedAt</c>; leaving it clears the stamp. Setting the same
    /// status is an idempotent no-op.</summary>
    Task<AdminSessionDetail> SetStatusAsync(
        Guid actorUserId, Guid id, SIMF.Common.Enums.SessionStatus status,
        CancellationToken cancellationToken = default);
}
