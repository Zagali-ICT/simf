using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Assets;

namespace SIMF.Application.Assets.Abstractions;

/// <summary>D-357 — the one service every entity uses to attach / read / remove a
/// unified media <c>Asset</c>. Centralises validation (size / content-type /
/// kind), out-of-row storage, the (Category, Owner) upsert, soft-delete and
/// audit in a single place. The generic asset endpoints and the Media Library
/// page are the only callers.</summary>
public interface IAssetService
{
    /// <summary>Attach (or replace) an uploaded file for (category, owner).
    /// Throws a validation <c>ApiException</c> on a bad size / content-type, or
    /// when <paramref name="kind"/> is <see cref="AssetKind.Video"/> (video is
    /// external-link only).</summary>
    Task SetUploadAsync(
        Guid actorUserId, AssetCategory category, Guid ownerId, AssetKind kind,
        byte[] content, string contentType, string? originalFileName,
        CancellationToken cancellationToken = default);

    /// <summary>Attach (or replace) an external-link asset for (category, owner).</summary>
    Task SetExternalLinkAsync(
        Guid actorUserId, AssetCategory category, Guid ownerId, AssetKind kind,
        string url, CancellationToken cancellationToken = default);

    /// <summary>Resolve the active asset for (category, owner) into everything a
    /// serve endpoint needs — bytes for an upload, the URL for a link — or
    /// <c>null</c> when there is no active asset.
    /// <para>A9 (security) — when <paramref name="requireOwnerActive"/> is
    /// <c>true</c> (the anonymous public serve) the resolve ALSO returns
    /// <c>null</c> if the owning entity (Speaker / Sponsor / News / …) has been
    /// soft-deleted, so a deactivated owner's image stops serving on its
    /// deterministic URL (the public list already hides it). The gated admin
    /// preview passes <c>false</c> so the Media Library can still show a
    /// deactivated owner's asset.</para></summary>
    Task<AssetResolution?> ResolveAsync(
        AssetCategory category, Guid ownerId,
        bool requireOwnerActive = true,
        CancellationToken cancellationToken = default);

    /// <summary>Of the given owners, which ones currently have an active asset in
    /// <paramref name="category"/> — resolved in a single batched query. Lets a
    /// grid render the real thumbnail only for rows that have one (and an
    /// initials / placeholder tile for the rest) without a per-row probe, keeping
    /// the (category → storage) mapping and the "active asset" predicate in this
    /// one service. Any list page (speakers, sponsors, media partners) can reuse
    /// it.</summary>
    Task<IReadOnlySet<Guid>> WhichOwnersHaveActiveAssetAsync(
        AssetCategory category, IReadOnlyCollection<Guid> ownerIds,
        CancellationToken cancellationToken = default);

    // -- Central Media Library management (D-357 / MediaLibrary.* permission) --

    /// <summary>One page of all assets (filter by category / kind / sourceType /
    /// isActive), newest first, with the owner name resolved per category.</summary>
    Task<GridPage<AdminAssetSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>One asset by id (any active state), or <c>null</c>.</summary>
    Task<AdminAssetSummary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete (deactivate) an asset by id. Idempotent.</summary>
    Task DeactivateAsync(Guid actorUserId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Restore a soft-deleted asset; 409 if another active asset already
    /// occupies its (category, owner) slot.</summary>
    Task RestoreAsync(Guid actorUserId, Guid id, CancellationToken cancellationToken = default);
}

/// <summary>How to serve an active asset: an <see cref="AssetSourceType.Upload"/>
/// carries <see cref="Content"/> + <see cref="ContentType"/>; an
/// <see cref="AssetSourceType.ExternalLink"/> carries <see cref="ExternalUrl"/>.</summary>
public sealed record AssetResolution(
    AssetSourceType SourceType,
    AssetKind Kind,
    byte[]? Content,
    string? ContentType,
    string? ExternalUrl);
