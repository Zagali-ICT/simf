import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The slice of the public session detail the Live broadcast screen needs
/// (Page_025). The session-detail repository (`features/sessions`) does NOT
/// expose the broadcast fields, so this tiny model decodes just the live
/// surface from the **same** wire contract (`GET /app/programme/sessions/{id}`,
/// `PublicSessionDetail`, append-only — D-219). All four broadcast fields are
/// camelCase on the wire; [status] is an int (0..3, frozen SessionStatus).
class LiveSession {
  const LiveSession({
    required this.title,
    required this.titleArabic,
    required this.status,
    required this.hasRecording,
    this.liveStreamUrl,
    this.liveSignLanguageUrl,
  });

  factory LiveSession.fromJson(Map<String, dynamic> json) {
    return LiveSession(
      title: (json['title'] as String?) ?? '',
      titleArabic: (json['titleArabic'] as String?) ?? '',
      status: (json['status'] as num?)?.toInt() ?? 0,
      hasRecording: json['hasRecording'] == true,
      liveStreamUrl: _trimToNull(json['liveStreamUrl'] as String?),
      liveSignLanguageUrl: _trimToNull(json['liveSignLanguageUrl'] as String?),
    );
  }

  final String title;
  final String titleArabic;

  /// Frozen `SessionStatus` int (Scheduled=0, Held=1, Recorded=2, Published=3).
  final int status;
  final bool hasRecording;

  /// The HLS/MP4 broadcast URL. Non-empty → the session is live / playable.
  final String? liveStreamUrl;

  /// The optional sign-language companion stream.
  final String? liveSignLanguageUrl;

  String localizedTitle(bool isArabic) {
    if (isArabic) {
      return titleArabic.isNotEmpty ? titleArabic : title;
    }
    return title.isNotEmpty ? title : titleArabic;
  }
}

/// Empty / whitespace-only strings collapse to null so the screen treats a
/// blank `liveStreamUrl` the same as a missing one (Page_025 L-2).
String? _trimToNull(String? value) {
  if (value == null) {
    return null;
  }
  final trimmed = value.trim();
  return trimmed.isEmpty ? null : trimmed;
}

/// Data layer for the Live broadcast screen (Page_025). One **anonymous** read
/// reusing the shipped public detail endpoint (no new API — D-271): decodes
/// only the broadcast slice into [LiveSession]. Throws [ApiFailure] on a wire
/// error — the screen maps a 404 to "not found" and any other failure to retry.
class LiveRepository {
  LiveRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/programme/sessions/{id}` → the live broadcast slice (E2).
  Future<LiveSession> getLiveSession(String sessionId) {
    return _client.get<LiveSession>(
      '/app/programme/sessions/$sessionId',
      decodeData: (data) => LiveSession.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }
}

final liveRepositoryProvider = Provider<LiveRepository>((ref) {
  return LiveRepository(ref.watch(simfApiClientProvider));
});
