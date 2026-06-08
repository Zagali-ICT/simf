/// D-349 — the Flutter mirror of the C# `LiveStreamUrlPolicy` (in
/// `src/Shared/SIMF.Common/LiveStreamUrlPolicy.cs`). Decides whether a live-feed
/// URL is accepted (YouTube link OR a direct HLS/MP4 stream) and, for a YouTube
/// link, extracts the video id the IFrame player needs. The two copies live in
/// different runtimes and are kept identical on purpose — change one, change the
/// other.
class YoutubeUrl {
  YoutubeUrl._();

  static const Set<String> _hosts = <String>{
    'youtube.com',
    'youtu.be',
    'youtube-nocookie.com',
  };

  /// True when [url] is an accepted live-feed URL: an absolute http/https URL
  /// that is either a YouTube link or a direct HLS/MP4 stream (`.m3u8`/`.mp4`).
  static bool isAllowedLiveUrl(String? url) {
    final uri = _parse(url);
    if (uri == null) {
      return false;
    }
    return _isYouTube(uri) || _isDirectStream(uri);
  }

  /// The YouTube video id for [url], or null when [url] is not a recognised
  /// YouTube link. Handles `watch?v=`, `youtu.be/<id>`, `/live/<id>`,
  /// `/embed/<id>` and `/shorts/<id>`.
  static String? tryParseId(String? url) {
    final uri = _parse(url);
    if (uri == null) {
      return null;
    }
    final host = _bareHost(uri);
    if (!_hosts.contains(host)) {
      return null;
    }
    if (host == 'youtu.be') {
      return _clean(uri.pathSegments.isEmpty ? null : uri.pathSegments.first);
    }
    final v = uri.queryParameters['v'];
    if (v != null && v.isNotEmpty) {
      return _clean(v);
    }
    final segments = uri.pathSegments;
    if (segments.length >= 2 &&
        (segments.first == 'live' ||
            segments.first == 'embed' ||
            segments.first == 'shorts')) {
      return _clean(segments[1]);
    }
    return null;
  }

  static Uri? _parse(String? url) {
    if (url == null || url.trim().isEmpty) {
      return null;
    }
    final uri = Uri.tryParse(url.trim());
    if (uri == null || !uri.hasScheme || !uri.hasAuthority) {
      return null;
    }
    if (uri.scheme != 'http' && uri.scheme != 'https') {
      return null;
    }
    return uri;
  }

  static String _bareHost(Uri uri) {
    var host = uri.host.toLowerCase();
    if (host.startsWith('www.')) {
      host = host.substring(4);
    } else if (host.startsWith('m.')) {
      host = host.substring(2);
    }
    return host;
  }

  static bool _isYouTube(Uri uri) => _hosts.contains(_bareHost(uri));

  static bool _isDirectStream(Uri uri) {
    final path = uri.path.toLowerCase();
    return path.endsWith('.m3u8') || path.endsWith('.mp4');
  }

  static String? _clean(String? id) {
    if (id == null) {
      return null;
    }
    final trimmed = id.trim();
    return trimmed.isEmpty ? null : trimmed;
  }
}
