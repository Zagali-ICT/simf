import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

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
    this.hallName,
    this.hallNameArabic,
    this.start,
    this.end,
    this.speakers = const <LiveSpeaker>[],
    this.liveCaptions,
    this.liveCaptionsArabic,
    this.liveNotice,
    this.liveNoticeArabic,
  });

  factory LiveSession.fromJson(Map<String, dynamic> json) {
    return LiveSession(
      title: (json['title'] as String?) ?? '',
      titleArabic: (json['titleArabic'] as String?) ?? '',
      status: (json['status'] as num?)?.toInt() ?? 0,
      hasRecording: json['hasRecording'] == true,
      liveStreamUrl: _trimToNull(json['liveStreamUrl'] as String?),
      liveSignLanguageUrl: _trimToNull(json['liveSignLanguageUrl'] as String?),
      // D-433 — the hall name, speakers + start are ALREADY on the shared
      // PublicSessionDetail wire (the live slice just had not decoded them).
      hallName: _trimToNull(json['hallName'] as String?),
      hallNameArabic: _trimToNull(json['hallNameArabic'] as String?),
      start: parseWireOrNull((json['start'] as String?) ?? ''),
      end: parseWireOrNull((json['end'] as String?) ?? ''),
      speakers: (json['speakers'] as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => LiveSpeaker.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false),
      // P5 — D-439: the AI live-caption text (null when none set).
      liveCaptions: _trimToNull(json['liveCaptions'] as String?),
      liveCaptionsArabic: _trimToNull(json['liveCaptionsArabic'] as String?),
      // FR-702 — the organiser's informational notice for this broadcast.
      liveNotice: _trimToNull(json['liveNotice'] as String?),
      liveNoticeArabic: _trimToNull(json['liveNoticeArabic'] as String?),
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

  // D-433 — broadcasting hall + the session's speakers/participants line
  // (frame 934:3450 header + 934:3617).
  final String? hallName;
  final String? hallNameArabic;
  final DateTime? start;

  /// S-3 — the session's scheduled end (UTC on the wire, decoded from the
  /// existing PublicSessionDetail.End). With [start] it defines the LIVE
  /// window: "live" = now within [start, end], not merely "a feed URL exists".
  /// Null when the wire omits it (the global main-live synthetic).
  final DateTime? end;
  final List<LiveSpeaker> speakers;

  // P5 — D-439 (Figma 934:3613): the AI live-caption / running-transcript line
  // rendered under the player. Bilingual; null when the admin has set none (the
  // strip then shows the placeholder hint and YouTube CC supplies captions).
  final String? liveCaptions;
  final String? liveCaptionsArabic;

  // FR-702 (owner 2026-07-31): the free-text notice the Control Panel authors
  // per session, shown as an informational banner ABOVE the player. It is a
  // NOTIFICATION, not a restriction — the owner reversed the earlier "Riyadh
  // region only" reading, so nothing here inspects the viewer's location and
  // nothing withholds the feed. Bilingual; null when the CP left it blank.
  final String? liveNotice;
  final String? liveNoticeArabic;

  String localizedTitle(bool isArabic) {
    if (isArabic) {
      return titleArabic.isNotEmpty ? titleArabic : title;
    }
    return title.isNotEmpty ? title : titleArabic;
  }

  String? localizedHall(bool isArabic) {
    final ar = (hallNameArabic ?? '').trim();
    final en = (hallName ?? '').trim();
    final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
    return value.isEmpty ? null : value;
  }

  /// P5 — D-439: the AI live-caption text in the active locale, falling back to
  /// the other when one side is blank. Null when neither is set (the strip then
  /// shows the placeholder hint).
  String? localizedCaption(bool isArabic) {
    final ar = (liveCaptionsArabic ?? '').trim();
    final en = (liveCaptions ?? '').trim();
    final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
    return value.isEmpty ? null : value;
  }

  /// FR-702 — the live notice in the active locale, falling back to the other
  /// language when one side is blank. Null when neither is set, and the banner
  /// is then not rendered at all.
  String? localizedNotice(bool isArabic) {
    final ar = (liveNoticeArabic ?? '').trim();
    final en = (liveNotice ?? '').trim();
    final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
    return value.isEmpty ? null : value;
  }

  /// The speakers/participants joined for the frame's "·"-bulleted line.
  String? localizedSpeakers(bool isArabic) {
    final names = speakers
        .map((s) => s.localized(isArabic))
        .where((n) => n.isNotEmpty)
        .toList(growable: false);
    return names.isEmpty ? null : names.join(' · ');
  }
}

/// One speaker/participant on the live session — the slice of
/// `PublicSessionSpeaker` the broadcast screen's participants line needs.
class LiveSpeaker {
  const LiveSpeaker({required this.name, required this.nameArabic});

  factory LiveSpeaker.fromJson(Map<String, dynamic> json) => LiveSpeaker(
        name: (json['name'] as String?) ?? '',
        nameArabic: (json['nameArabic'] as String?) ?? '',
      );

  final String name;
  final String nameArabic;

  String localized(bool isArabic) {
    final ar = nameArabic.trim();
    final en = name.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }
}

/// One upcoming session card (frame 934:3621/3630 "الجلسات القادمة") — the slice
/// of `PublicSessionListItem` the live screen needs to show the next sessions.
class UpcomingSession {
  const UpcomingSession({
    required this.id,
    required this.title,
    required this.titleArabic,
    required this.start,
  });

  factory UpcomingSession.fromJson(Map<String, dynamic> json) => UpcomingSession(
        id: (json['id'] as String?) ?? '',
        title: (json['title'] as String?) ?? '',
        titleArabic: (json['titleArabic'] as String?) ?? '',
        start:
            parseWireOrNull((json['start'] as String?) ?? ''),
      );

  final String id;
  final String title;
  final String titleArabic;
  final DateTime? start;

  String localizedTitle(bool isArabic) {
    final ar = titleArabic.trim();
    final en = title.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
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

  /// D-433 — the "الجلسات القادمة" cards: reuse the shipped agenda list
  /// (`GET /app/programme/sessions`, anonymous), keep only sessions starting in
  /// the future, soonest first, and excluding [excludeSessionId] (the one being
  /// watched). Returns at most [take].
  Future<List<UpcomingSession>> getUpcomingSessions({
    String? excludeSessionId,
    int take = 3,
  }) {
    return _client.get<List<UpcomingSession>>(
      '/app/programme/sessions',
      decodeData: (data) {
        final items = (data is Map ? data['items'] : null) as List? ??
            const <dynamic>[];
        final now = saudiNow();
        final upcoming = items
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => UpcomingSession.fromJson(e.cast<String, dynamic>()))
            .where((s) =>
                s.id != excludeSessionId &&
                s.start != null &&
                s.start!.isAfter(now),)
            .toList()
          ..sort((a, b) => a.start!.compareTo(b.start!));
        return upcoming.take(take).toList(growable: false);
      },
    );
  }
}

final liveRepositoryProvider = Provider<LiveRepository>((ref) {
  return LiveRepository(ref.watch(simfApiClientProvider));
});
