using SIMF.Contracts.Cms;

namespace SIMF.Application.Cms.Abstractions;

/// <summary>
/// D-173 (gap doc G8, PDF §1) — public (anonymous) read surface for
/// content blocks + active banners. Hot path on every Flutter app
/// + Website page render — must stay cheap.
/// </summary>
public interface IPublicCmsService
{
    /// <summary>Returns one content block by key, or null when the
    /// key is missing or the row is inactive.</summary>
    Task<PublicContentBlock?> GetContentBlockAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>Batch-resolve multiple keys in one round trip. Missing /
    /// inactive keys are silently omitted from the result map.</summary>
    Task<PublicContentBlockBatch> GetContentBlocksAsync(
        IReadOnlyList<string> keys, CancellationToken cancellationToken = default);

    /// <summary>Returns banners that are active AND within their
    /// [Start, End] window at the moment of the call.</summary>
    Task<PublicBanners> GetActiveBannersAsync(
        CancellationToken cancellationToken = default);
}
