using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.Delegations.Abstractions;

/// <summary>D-174 (gap doc G11, Mockup page 21) — admin CRUD over
/// delegations.</summary>
public interface IAdminDelegationService
{
    Task<GridPage<AdminDelegationSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminDelegationSummary?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminDelegationSummary> CreateAsync(
        Guid actorUserId, CreateDelegationRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminDelegationSummary> UpdateAsync(
        Guid actorUserId, Guid id, UpdateDelegationRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);
}
