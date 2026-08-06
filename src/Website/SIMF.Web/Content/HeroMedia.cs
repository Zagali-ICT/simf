using Microsoft.Extensions.Caching.Memory;
using SIMF.ApiClient;
using SIMF.Common;

namespace SIMF.Web.Content;

/// <summary>The landing hero background video, classified for rendering: a
/// YouTube link becomes an embed <see cref="YouTubeId"/> (iframe); a direct
/// MP4/HLS link becomes a <see cref="FileUrl"/> (&lt;video&gt;). Exactly one is
/// non-null.</summary>
public sealed record HeroVideoSource(string? YouTubeId, string? FileUrl);

/// <summary>
/// Resolves the CP-editable hero-section background video
/// (<c>OrganizationProfile.BackgroundVideoUrl</c>) once per short cache window and
/// classifies it (YouTube id vs direct file) with the shared
/// <see cref="LiveStreamUrlPolicy"/>, so the landing hero plays the configured
/// video instead of the bundled <c>hero-video.mp4</c>. A null / unreachable
/// profile, an unset URL, or an unrecognised URL yields <c>null</c> and the caller
/// falls back to the bundled asset — the hero never blanks, and a transient API
/// outage is not cached.
/// </summary>
public sealed class HeroMedia(SimfPublicClient api, IMemoryCache cache)
{
    private const string CacheKey = "simf.web.hero-background-video";
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    /// <summary>The configured hero background video classified for rendering, or
    /// <c>null</c> when none is set / the profile is unavailable (the caller then
    /// uses the bundled <c>hero-video.mp4</c>).</summary>
    public async Task<HeroVideoSource?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out HeroVideoSource? cached))
        {
            return cached;
        }

        var profile = await api.GetOrganizationProfileAsync(cancellationToken);
        var source = Classify(profile?.BackgroundVideoUrl);
        if (source is null)
        {
            // A miss is deliberately NOT cached, so a transient outage retries.
            return null;
        }

        cache.Set(CacheKey, source, CacheFor);
        return source;
    }

    /// <summary>Classifies a raw background-video URL: a well-formed YouTube link →
    /// its 11-char video id (iframe); a direct https MP4/HLS stream → the file URL
    /// (&lt;video&gt;); anything else (blank, a non-stream page, a YouTube link with
    /// no id) → <c>null</c> (fall back to the bundled asset). Pure + static so it is
    /// unit-testable without the API.</summary>
    public static HeroVideoSource? Classify(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }
        if (LiveStreamUrlPolicy.TryGetYouTubeVideoId(url, out var id))
        {
            return new HeroVideoSource(id, null);
        }
        return LiveStreamUrlPolicy.IsAllowed(url) ? new HeroVideoSource(null, url.Trim()) : null;
    }
}
