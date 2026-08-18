import 'package:flutter/foundation.dart';
import 'package:simf_app/core/utils/bilingual.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/sessions/data/session_enums.dart';
import 'package:simf_app/features/sessions/data/session_lifecycle.dart';
import 'package:simf_app/features/sessions/data/session_speaker.dart';

// Day grouping moved to core/utils (cross-feature); re-exported so the
// existing `distinctLocalDays` / `sameLocalDay` call sites in session_filters,
// ai_summary and presentations keep resolving off this file.
export 'package:simf_app/core/utils/local_days.dart';

/// One row in the cached programme — mirrors
/// `SIMF.Contracts.Programme.PublicSessionListItem` (`GET
/// /app/programme/sessions`).
/// [start]/[end] are zone-free on the wire — the UI renders device-local
/// (Page_016 L-3/L-8). Bilingual fields are paired; [categoryName] is the
/// "main session" / type tag (null until the team seeds the category list —
/// L-4); [speakers] is always a list (empty, never null — L-7) and carries the
/// D-271 flag + photo for the detail preview.
@immutable
class SessionListItem {
  const SessionListItem({
    required this.id,
    required this.code,
    required this.title,
    required this.titleArabic,
    required this.hallId,
    required this.hallName,
    required this.hallNameArabic,
    required this.start,
    required this.end,
    required this.status,
    required this.speakers,
    this.description,
    this.descriptionArabic,
    this.categoryId,
    this.categoryName,
    this.categoryNameArabic,
    this.primaryThemeColor,
    this.type,
    this.hasPublishedSummary = false,
  });

  factory SessionListItem.fromJson(Map<String, dynamic> json) =>
      SessionListItem(
        id: json['id'] as String? ?? '',
        code: json['code'] as String? ?? '',
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        hallId: json['hallId'] as String? ?? '',
        hallName: json['hallName'] as String? ?? '',
        hallNameArabic: json['hallNameArabic'] as String? ?? '',
        start: parseWireDateTime(json['start'], 'start'),
        end: parseWireDateTime(json['end'], 'end'),
        status: SessionStatus.fromJson(json['status']),
        speakers: _decodeSpeakers(json['speakers']),
        description: json['description'] as String?,
        descriptionArabic: json['descriptionArabic'] as String?,
        categoryId: json['categoryId'] as String?,
        categoryName: json['categoryName'] as String?,
        categoryNameArabic: json['categoryNameArabic'] as String?,
        primaryThemeColor: json['primaryThemeColor'] as String?,
        type: SessionType.fromJson(json['type']),
        hasPublishedSummary: json['hasPublishedSummary'] as bool? ?? false,
      );

  final String id;
  final String code;
  final String title;
  final String titleArabic;
  final String hallId;
  final String hallName;
  final String hallNameArabic;
  final DateTime start;
  final DateTime end;
  final SessionStatus status;
  final List<SessionSpeaker> speakers;
  // D-452 (Figma 883:2308): the session's type, driving the type tabs.
  final SessionType? type;
  final String? description;
  final String? descriptionArabic;
  final String? categoryId;
  final String? categoryName;
  final String? categoryNameArabic;
  final String? primaryThemeColor;

  /// A8 (D-237, wire `hasPublishedSummary`) — true when this session has an
  /// active summary carrying a PublishedAt stamp (the محضر the app renders).
  /// Its
  /// OWN editorial publish state, orthogonal to [status]; drives whether the
  /// session belongs in the summaries list + the "summary ready" badge, without
  /// a per-session `/summary` probe.
  final bool hasPublishedSummary;

  /// The session's start on the Saudi event-local wall clock (zone-free on the
  /// wire).
  DateTime get startLocal => saudiOf(start);

  SessionPhase phase(DateTime nowUtc) => sessionPhase(start, end, nowUtc);

  /// The session's end on the Saudi event-local wall clock — drives the agenda
  /// time-rail's bottom value (Figma 883:2308) and the summary duration
  /// (1072:13518).
  DateTime get endLocal => saudiOf(end);

  /// The session length in whole minutes, floored at 0 — the summary card's
  /// duration label (Figma 1072:13518). Mirrors
  /// `MyAreaSessionItem.durationMinutes`.
  int get durationMinutes {
    final minutes = end.difference(start).inMinutes;
    return minutes < 0 ? 0 : minutes;
  }

  String localizedTitle({required bool isArabic}) =>
      pickLocalized(titleArabic, title, isArabic: isArabic);

  String? localizedHall({required bool isArabic}) =>
      pickLocalizedOrNull(hallNameArabic, hallName, isArabic: isArabic);

  String? localizedDescription({required bool isArabic}) =>
      pickLocalizedOrNull(descriptionArabic, description, isArabic: isArabic);

  String? localizedCategory({required bool isArabic}) =>
      pickLocalizedOrNull(categoryNameArabic, categoryName, isArabic: isArabic);
}

/// The envelope for the cached programme (`PublicSessions = { items: [...] }`).
/// Kept as a tiny wrapper so the repository reads `items` from one place.
@immutable
class SessionsPage {
  const SessionsPage(this.items);

  factory SessionsPage.fromJson(Object? data) {
    final list =
        (data is Map ? data['items'] : null) as List? ?? const <dynamic>[];
    final items = list
        .whereType<Map<dynamic, dynamic>>()
        .map((e) => SessionListItem.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false);
    return SessionsPage(items);
  }

  final List<SessionListItem> items;
}

/// The full detail for one session — mirrors
/// `SIMF.Contracts.Programme.PublicSessionDetail` (`GET
/// /app/programme/sessions/{id}`,
/// anonymous). Page_017 renders the header (code/time/title), the description,
/// the ordered speaker cards (each with the D-271 country flag + photo, reusing
/// [SessionSpeaker]), and — per Figma 889:2450 — a **رابط الجلسة** button when
/// the session has a live feed ([liveStreamUrl] non-null) that opens the live
/// screen (25). The remaining detail fields (themes, the seat-availability
/// summary, the recording URL) belong to the seat / live screens and are not
/// decoded here.
@immutable
class SessionDetail {
  const SessionDetail({
    required this.id,
    required this.code,
    required this.title,
    required this.titleArabic,
    required this.hallId,
    required this.hallName,
    required this.hallNameArabic,
    required this.start,
    required this.end,
    required this.speakers,
    this.description,
    this.descriptionArabic,
    this.categoryId,
    this.categoryName,
    this.categoryNameArabic,
    this.type,
    this.liveStreamUrl,
    this.displayOrder = 0,
    this.arrivalGraceMinutes = defaultArrivalGraceMinutes,
  });

  factory SessionDetail.fromJson(Map<String, dynamic> json) => SessionDetail(
        id: json['id'] as String? ?? '',
        code: json['code'] as String? ?? '',
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        hallId: json['hallId'] as String? ?? '',
        hallName: json['hallName'] as String? ?? '',
        hallNameArabic: json['hallNameArabic'] as String? ?? '',
        start: parseWireDateTime(json['start'], 'start'),
        end: parseWireDateTime(json['end'], 'end'),
        speakers: _decodeSpeakers(json['speakers']),
        description: json['description'] as String?,
        descriptionArabic: json['descriptionArabic'] as String?,
        categoryId: json['categoryId'] as String?,
        categoryName: json['categoryName'] as String?,
        categoryNameArabic: json['categoryNameArabic'] as String?,
        type: SessionType.fromJson(json['type']),
        liveStreamUrl: json['liveStreamUrl'] as String?,
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
        arrivalGraceMinutes: (json['arrivalGraceMinutes'] as num?)?.toInt() ??
            defaultArrivalGraceMinutes,
      );

  final String id;
  final String code;
  final String title;
  final String titleArabic;
  final String hallId;
  final String hallName;
  final String hallNameArabic;
  final DateTime start;
  final DateTime end;
  final List<SessionSpeaker> speakers;
  final String? description;
  final String? descriptionArabic;
  final String? categoryId;
  final String? categoryName;
  final String? categoryNameArabic;

  /// #29 — the session's kind (the same wire field the list carries, D-452). A
  /// **workshop** detail is reduced to its title + time only (no description,
  /// speakers, seat/join block or live/summary actions). Null on an older API
  /// or an untyped session, which renders the full detail as before.
  final SessionType? type;

  /// The live-broadcast feed URL (YouTube / direct HLS·MP4 — D-349), or null
  /// when the session has no live feed. Drives the Figma 889:2450 **رابط
  /// الجلسة** button: shown only when non-null, opening the live screen (25).
  final String? liveStreamUrl;

  /// True when the session has a live feed the app can open (the رابط الجلسة
  /// button's visibility gate).
  bool get hasLiveStream =>
      liveStreamUrl != null && liveStreamUrl!.trim().isNotEmpty;

  /// The session's time-phase (upcoming / live / ended) against [nowUtc] — the
  /// header buttons gate off this (summary = ended; live = live +
  /// hasLiveStream).
  SessionPhase phase(DateTime nowUtc) => sessionPhase(start, end, nowUtc);

  /// The session's 1-based position within its day (D-567, Figma 889:2604) —
  /// the gold index badge shows it zero-padded ("02"). 0 = unknown (an older
  /// API), in which case the badge falls back to the [code].
  final int displayOrder;

  /// D-840 — how many minutes before the start (and after the end) the
  /// SERVER will accept an arrival for this session, already resolved by it
  /// (the session's own override, else its hall's, else the global value).
  ///
  /// Read from the wire rather than assumed: D-839 made the grace configurable
  /// per hall and per session, so there is no single server constant left to
  /// mirror by hand. Falls back to [defaultArrivalGraceMinutes] on an older
  /// API — the value this screen used to hard-code — so nothing changes until
  /// the team actually configures a hall.
  final int arrivalGraceMinutes;

  /// The grace the system used before it was configurable, and the value an
  /// older API implies by omitting the field.
  static const int defaultArrivalGraceMinutes = 15;

  Duration get arrivalGrace => Duration(minutes: arrivalGraceMinutes);

  DateTime get startLocal => saudiOf(start);
  DateTime get endLocal => saudiOf(end);

  String localizedTitle({required bool isArabic}) =>
      pickLocalized(titleArabic, title, isArabic: isArabic);

  String localizedHall({required bool isArabic}) =>
      pickLocalized(hallNameArabic, hallName, isArabic: isArabic);

  String? localizedDescription({required bool isArabic}) =>
      pickLocalizedOrNull(descriptionArabic, description, isArabic: isArabic);

  String? localizedCategory({required bool isArabic}) =>
      pickLocalizedOrNull(categoryNameArabic, categoryName, isArabic: isArabic);
}

/// Decodes a `speakers[]` array (shared by the list item + the detail). A
/// missing / null array decodes to an empty list — never null on the wire
/// (L-7).
List<SessionSpeaker> _decodeSpeakers(Object? data) =>
    (data as List? ?? const <dynamic>[])
        .whereType<Map<dynamic, dynamic>>()
        .map((e) => SessionSpeaker.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false);
