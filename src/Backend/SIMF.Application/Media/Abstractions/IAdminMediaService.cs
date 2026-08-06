using SIMF.Common;
using SIMF.Contracts.Media;

namespace SIMF.Application.Media.Abstractions;

/// <summary>Admin CRUD over <c>MediaItem</c> (Mockup page 30).
/// Mirrors <c>IAdminSpeakerService</c>: Guid key, soft-delete via
/// <c>DeactivateAsync</c>, audit on every mutation.</summary>
public interface IAdminMediaService
{
    Task<GridPage<AdminMediaSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminMediaDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminMediaDetail> CreateAsync(
        Guid actorUserId, AdminCreateMediaRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminMediaDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateMediaRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>Persists the uploaded image bytes out-of-row (D-90) and
    /// records the returned relative path on the item. Returns the updated
    /// detail.</summary>
    Task<AdminMediaDetail> SetImageAsync(
        Guid actorUserId, Guid id, byte[] content, string contentType,
        CancellationToken cancellationToken = default);
}
