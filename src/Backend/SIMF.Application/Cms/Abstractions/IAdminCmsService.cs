using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.Cms.Abstractions;

/// <summary>
/// Admin CRUD over content blocks +
/// banners. Distinct from the public read surface
/// (<see cref="IPublicCmsService"/>) which is anonymous + cache-aware.
/// </summary>
public interface IAdminCmsService
{
    Task<GridPage<AdminContentBlockSummary>> ListContentBlocksAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminContentBlockSummary?> GetContentBlockAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>Upsert by <see cref="UpsertContentBlockRequest.Key"/>.
    /// Creates the row when missing, updates in place when present.
    /// Bumps <c>LastUpdatedAt</c> + <c>LastUpdatedByUserId</c>.</summary>
    Task<AdminContentBlockSummary> UpsertContentBlockAsync(
        Guid actorUserId,
        UpsertContentBlockRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateContentBlockAsync(
        Guid actorUserId,
        string key,
        CancellationToken cancellationToken = default);

    Task<GridPage<AdminBannerSummary>> ListBannersAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminBannerDetail?> GetBannerAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminBannerDetail> CreateBannerAsync(
        Guid actorUserId,
        CreateBannerRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminBannerDetail> UpdateBannerAsync(
        Guid actorUserId,
        Guid id,
        UpdateBannerRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateBannerAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default);
}
