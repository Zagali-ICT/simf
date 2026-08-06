using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.Profiles;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Persistence seam for the Interests CRUD. The
/// Application <see cref="InterestService"/> talks to this contract; the
/// Infrastructure implementation owns the EF query shapes. Mirrors the
/// <see cref="Notifications.INotificationRepository"/> idiom — read shapes
/// return the wire DTOs, mutations return the tracked <see cref="UserInterest"/>
/// so the service can mutate-then-<see cref="SaveChangesAsync"/>.
/// </summary>
public interface IInterestRepository
{
    /// <summary>Active interests for the visitor picker — ordered by
    /// <c>DisplayOrder</c>, tie-broken by <c>Name</c>.</summary>
    Task<IReadOnlyList<InterestDto>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>One page of the admin grid: applies the search / filter /
    /// sort of <paramref name="query"/> then <c>Skip</c>/<c>Take</c>.
    /// Returns the page plus the unpaged total.</summary>
    Task<(IReadOnlyList<AdminInterestSummary> Items, int Total)> ListPageAsync(
        GridQuery query, int skip, int top,
        CancellationToken cancellationToken = default);

    /// <summary>One row as the admin summary, or null when not found.</summary>
    Task<AdminInterestSummary?> GetSummaryAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when an interest already carries <paramref name="name"/>.
    /// When <paramref name="excludingId"/> is set, that row is ignored — used
    /// for the rename-clash check so a row never clashes with itself.</summary>
    Task<bool> NameExistsAsync(
        string name, Guid? excludingId,
        CancellationToken cancellationToken = default);

    /// <summary>The tracked row for mutation, or null when not found.</summary>
    Task<UserInterest?> FindAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a new interest and persists it.</summary>
    Task AddAsync(UserInterest interest, CancellationToken cancellationToken = default);

    /// <summary>Persists pending mutations on tracked rows.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
