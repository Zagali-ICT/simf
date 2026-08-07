using SIMF.Common;
using SIMF.Contracts.Faq;

namespace SIMF.Application.Faq.Abstractions;

/// <summary>
/// Admin CRUD over the two-level FAQ (groups → entries).
/// Built on <c>SimfAppDbContext</c>; one audit row per mutation; soft-delete
/// via <c>IsActive</c>. Mirrors the News module shape.
/// </summary>
public interface IAdminFaqService
{
    // -- Groups --
    Task<GridPage<AdminFaqGroupSummary>> ListGroupsAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminFaqGroupSummary?> GetGroupAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminFaqGroupSummary> CreateGroupAsync(
        Guid actorUserId, CreateFaqGroupRequest request, CancellationToken cancellationToken = default);

    Task<AdminFaqGroupSummary> UpdateGroupAsync(
        Guid actorUserId, Guid id, UpdateFaqGroupRequest request, CancellationToken cancellationToken = default);

    Task DeactivateGroupAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default);

    // -- Entries --
    Task<GridPage<AdminFaqEntrySummary>> ListEntriesAsync(
        Guid groupId, GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminFaqEntrySummary?> GetEntryAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminFaqEntrySummary> CreateEntryAsync(
        Guid actorUserId, CreateFaqEntryRequest request, CancellationToken cancellationToken = default);

    Task<AdminFaqEntrySummary> UpdateEntryAsync(
        Guid actorUserId, Guid id, UpdateFaqEntryRequest request, CancellationToken cancellationToken = default);

    Task DeactivateEntryAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default);
}
