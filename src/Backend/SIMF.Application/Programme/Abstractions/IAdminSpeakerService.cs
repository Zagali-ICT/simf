using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>Admin CRUD over <c>Speaker</c> (SIMF-DAT-001 §5.4).</summary>
public interface IAdminSpeakerService
{
    Task<GridPage<AdminSpeakerSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminSpeakerDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminSpeakerDetail> CreateAsync(
        Guid actorUserId, AdminCreateSpeakerRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminSpeakerDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateSpeakerRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);
}
