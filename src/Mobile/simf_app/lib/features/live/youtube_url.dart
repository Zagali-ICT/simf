/// D-349 — the Flutter mirror of the C# `LiveStreamUrlPolicy` (in
/// `src/Shared/SIMF.Common/LiveStreamUrlPolicy.cs`). Decides whether a live-feed
/// URL is accepted (a YouTube link with a well-formed video id OR a direct
/// HLS/MP4 stream) and, for a YouTube link, extracts the video id the IFrame
/// player needs. The two copies live in different runtimes and are kept
/// equivalent on purpose — change one, change the other.
class YoutubeUrl {
  YoutubeUrl._();

  static const Set<String> _hosts = <String>{
    'youtube.com',
    'youtu.be',
    'youtube-nocookie.com',
  };

  /// A YouTube video id is exactly 11 URL-safe characters.
  static final RegExp _idPattern = RegExp(r'^[A-Za-z0-9_-]{11}$');

  /// True when [url] is an accepted live-feed URL: a YouTube link with a valid
  /// video id, OR a direct HLS/MP4 stream (`.m3u8`/`.mp4`). Mirrors the C#
  /// `LiveStreamUrlPolicy.IsAllowed`.
  static bool isAllowedLiveUrl(String? url) {
    return tryParseId(url) != null || _isDirectStream(url);
  }

  /// The 11-character YouTube video id for [url], or null when [url] is not a
  /// YouTube link with an extractable, well-formed id. Handles `watch?v=`,
  /// `youtu.be/<id>`, `/live/<id>`, `/embed/<id>` and `/shorts/<id>`. A YouTube
  /// host with no id (a channel/handle/feed link) or a malformed id returns null.
  static String? tryParseId(String? url) {
    final uri = _parse(url);
    if (uri == null) {
      return null;
    }
    final host = _bareHost(uri);
    if (!_hosts.contains(host)) {
      return null;
    }
    final candidate = _clean(_extractIdCandidate(uri, host));
    if (candidate == null || !_idPattern.hasMatch(candidate)) {
      return null;
    }
    return candidate;
  }

  static String? _extractIdCandidate(Uri uri, String host) {
    if (host == 'youtu.be') {
      return uri.pathSegments.isEmpty ? null : uri.pathSegments.first;
    }
    final v = uri.queryParameters['v'];
    if (v != null && v.isNotEmpty) {
      return v;
    }
    final segments = uri.pathSegments;
    if (segments.length >= 2 &&
        (segments.first == 'live' ||
            segments.first == 'embed' ||
            segments.first == 'shorts')) {
      return segments[1];
    }
    return null;
  }

  static bool _isDirectStream(String? url) {
    final uri = _parse(url);
    if (uri == null) {
      return false;
    }
    // Use the decoded last path segment so this matches the C# side, which
    // checks the decoded AbsolutePath (e.g. an encoded "%2Emp4" → ".mp4").
    final segments = uri.pathSegments;
    final last = (segments.isEmpty ? '' : segments.last).toLowerCase();
    return last.endsWith('.m3u8') || last.endsWith('.mp4');
  }

  static Uri? _parse(String? url) {
    if (url == null || url.trim().isEmpty) {
      return null;
    }
    final uri = Uri.tryParse(url.trim());
    if (uri == null || !uri.hasScheme || !uri.hasAuthority) {
      return null;
    }
    // https only — cleartext http is rejected so a feed cannot be downgraded /
    // man-in-the-middled (D-349 security hardening; mirrors
    // LiveStreamUrlPolicy).
    if (uri.scheme != 'https') {
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

  static String? _clean(String? id) {
    if (id == null) {
      return null;
    }
    final trimmed = id.trim();
    return trimmed.isEmpty ? null : trimmed;
  }
}
