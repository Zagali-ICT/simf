namespace SIMF.Common;

/// <summary>
/// D-349 — the single rule that decides which live-broadcast feed URLs SIMF
/// accepts. The live-video provider is <b>YouTube</b> (proof of concept), with
/// a direct <b>HLS/MP4</b> stream kept as a fallback. A YouTube URL is accepted
/// only when a well-formed 11-character video id can be extracted (a channel /
/// handle / feed link with no id is rejected, since the player needs the id).
/// An admin pastes the URL into the CP Session form; both the CP form
/// (client-side) and <c>AdminSessionService</c> (server-side) call
/// <see cref="IsAllowed"/>, so the rule lives in exactly one place.
///
/// <para>The Flutter live screen mirrors this rule in
/// <c>src/Mobile/simf_app/lib/features/live/youtube_url.dart</c> — a separate
/// runtime, so the duplicate is unavoidable and is kept equivalent on purpose
/// (see DECISIONS_LOG D-349). Change one, change the other.</para>
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
    /// http/https URL that is either a YouTube link with a well-formed video id
    /// or a direct HLS/MP4 stream (path ending <c>.m3u8</c> or <c>.mp4</c>).
    /// A blank URL is NOT allowed — callers treat blank as "no feed" and skip the
    /// check, so this method only ever sees a value the admin actually typed.
    /// </summary>
    public static bool IsAllowed(string? url)
    {
        if (!TryParseHttpUrl(url, out var uri))
        {
            return false;
        }
        return TryGetYouTubeVideoId(uri, out _) || IsDirectStream(uri);
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

    // Mirrors the Flutter YoutubeUrl.tryParseId: a YouTube host AND an
    // extractable, well-formed 11-char id. Handles watch?v=, youtu.be/<id>,
    // /live/<id>, /embed/<id> and /shorts/<id>.
    private static bool TryGetYouTubeVideoId(Uri uri, out string videoId)
    {
        videoId = string.Empty;
        var host = BareHost(uri);
        if (!YouTubeHosts.Contains(host))
        {
            return false;
        }
        var candidate = ExtractIdCandidate(uri, host)?.Trim();
        if (string.IsNullOrEmpty(candidate) || !IsWellFormedId(candidate))
        {
            return false;
        }
        videoId = candidate;
        return true;
    }

    private static string? ExtractIdCandidate(Uri uri, string host)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (host == "youtu.be")
        {
            return segments.Length >= 1 ? segments[0] : null;
        }
        var v = GetQueryValue(uri, "v");
        if (!string.IsNullOrEmpty(v))
        {
            return v;
        }
        return segments.Length >= 2
            && (segments[0] == "live" || segments[0] == "embed" || segments[0] == "shorts")
            ? segments[1]
            : null;
    }

    private static string BareHost(Uri uri)
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
        return host;
    }

    private static string? GetQueryValue(Uri uri, string key)
    {
        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }
            if (string.Equals(Uri.UnescapeDataString(pair[..eq]), key, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }
        return null;
    }

    private static bool IsWellFormedId(string id)
    {
        if (id.Length != 11)
        {
            return false;
        }
        foreach (var c in id)
        {
            var ok = c is (>= 'A' and <= 'Z')
                or (>= 'a' and <= 'z')
                or (>= '0' and <= '9')
                or '_'
                or '-';
            if (!ok)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsDirectStream(Uri uri)
    {
        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.EndsWith(".m3u8", StringComparison.Ordinal)
            || path.EndsWith(".mp4", StringComparison.Ordinal);
    }
}
