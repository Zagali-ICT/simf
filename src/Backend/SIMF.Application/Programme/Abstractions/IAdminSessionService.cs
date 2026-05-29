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
}
