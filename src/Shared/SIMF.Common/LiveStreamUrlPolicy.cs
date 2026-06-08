namespace SIMF.Common;

/// <summary>
/// D-349 — the single rule that decides which live-broadcast feed URLs SIMF
/// accepts. The live-video provider is <b>YouTube</b> (proof of concept), with
/// a direct <b>HLS/MP4</b> stream kept as a fallback. An admin pastes the URL
/// into the CP Session form; both the CP form (client-side) and
/// <c>AdminSessionService</c> (server-side) call <see cref="IsAllowed"/>, so the
/// rule lives in exactly one place.
///
/// <para>The Flutter live screen mirrors this rule in
/// <c>src/Mobile/simf_app/lib/features/live/youtube_url.dart</c> — a separate
/// runtime, so the duplicate is unavoidable and is kept byte-for-byte equivalent
/// on purpose (see DECISIONS_LOG D-349). Change one, change the other.</para>
/// </summary>
public static class LiveStreamUrlPolicy
{
    // Hosts (lower-cased, with any leading "www." / "m." stripped) that mark a
    // URL as a YouTube link. youtube-nocookie.com is YouTube's privacy-embed host.
    private static readonly HashSet<string> YouTubeHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com",
        "youtu.be",
        "youtube-nocookie.com",
    };

    /// <summary>
    /// True when <paramref name="url"/> is an accepted live-feed URL: an absolute
    /// http/https URL that is either a YouTube link or a direct HLS/MP4 stream
    /// (path ending <c>.m3u8</c> or <c>.mp4</c>).
    /// A blank URL is NOT allowed — callers treat blank as "no feed" and skip the
    /// check, so this method only ever sees a value the admin actually typed.
    /// </summary>
    public static bool IsAllowed(string? url)
    {
        if (!TryParseHttpUrl(url, out var uri))
        {
            return false;
        }
        return IsYouTube(uri) || IsDirectStream(uri);
    }

    private static bool TryParseHttpUrl(string? url, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }
        uri = parsed;
        return true;
    }

    private static bool IsYouTube(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }
        else if (host.StartsWith("m.", StringComparison.Ordinal))
        {
            host = host[2..];
        }
        return YouTubeHosts.Contains(host);
    }

    private static bool IsDirectStream(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.EndsWith(".m3u8", StringComparison.Ordinal)
            || path.EndsWith(".mp4", StringComparison.Ordinal);
    }
}
