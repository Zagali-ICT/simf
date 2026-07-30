using SIMF.Common;
using SIMF.Contracts.Exhibitors;

namespace SIMF.Application.Exhibitors.Abstractions;

/// <summary>D-199 #3 — admin CRUD over exhibitors plus account provisioning.
/// The owner model: create the exhibitor name first, then provision login
/// accounts under it.</summary>
public interface IAdminExhibitorService
{
    Task<GridPage<AdminExhibitorSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminExhibitorDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminExhibitorDetail> CreateAsync(
        Guid actorUserId, CreateExhibitorRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminExhibitorDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateExhibitorRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExhibitorAccountSummary>> ListAccountsAsync(
        Guid exhibitorId, CancellationToken cancellationToken = default);

    Task<ExhibitorAccountSummary> ProvisionAccountAsync(
        Guid actorUserId, Guid exhibitorId, ProvisionExhibitorAccountRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>D-781 — attach an EXISTING exhibitor-typed account to this
    /// exhibitor. Provisioning is the only other writer of
    /// <c>ExhibitorMembership</c>, so an account created through the generic
    /// Others pipeline had no membership and was locked out of the booth tools
    /// with no admin path to fix it.</summary>
    Task<ExhibitorAccountSummary> LinkAccountAsync(
        Guid actorUserId, Guid exhibitorId, LinkExhibitorAccountRequest request,
        CancellationToken cancellationToken = default);
}
