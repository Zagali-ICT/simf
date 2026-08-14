import 'package:flutter/foundation.dart';
import 'package:simf_app/core/utils/local_days.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/sessions/data/session_lifecycle.dart';

// Day grouping moved to core/utils (cross-feature); re-exported so the
// existing `distinctLocalDays` / `sameLocalDay` call sites here and in
// ai_summary and presentations keep resolving off this file.
export 'package:simf_app/core/utils/local_days.dart';

/// The session broadcast lifecycle — mirrors `SIMF.Common.Enums.SessionStatus`
/// (frozen, int-backed: Scheduled=0, Held=1, Recorded=2, Published=3). The wire
/// value is an **int** — there is no string-enum converter anywhere in the API
/// (verified D-299), so the JSON carries `0..3`, not the name. [fromJson]
/// decodes tolerantly (int OR name; unknown → [scheduled]) per the append-only
/// wire rule (D-219).
enum SessionStatus {
  scheduled(0, 'Scheduled'),
  held(1, 'Held'),
  recorded(2, 'Recorded'),
  published(3, 'Published');

  const SessionStatus(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  static SessionStatus fromJson(Object? value) {
    if (value is String) {
      for (final status in values) {
        if (status.wireName == value) {
          return status;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final status in values) {
        if (status.wireValue == asInt) {
          return status;
        }
      }
    }
    return SessionStatus.scheduled;
  }
}

/// The role a speaker plays in a session — mirrors
/// `SIMF.Common.Enums.SessionSpeakerRole` (frozen, int-backed: Speaker=0,
/// Host=1). Int on the wire; [fromJson] tolerant (int OR name; unknown →
/// [speaker]).
enum SessionSpeakerRole {
  speaker(0, 'Speaker'),
  host(1, 'Host');

  const SessionSpeakerRole(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  static SessionSpeakerRole fromJson(Object? value) {
    if (value is String) {
      for (final role in values) {
        if (role.wireName == value) {
          return role;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final role in values) {
        if (role.wireValue == asInt) {
          return role;
        }
      }
    }
    return SessionSpeakerRole.speaker;
  }
}

/// D-452 (Figma 883:2308 type tabs) — the kind of a session, driving the app's
/// "جلسات / ورش العمل" tabs (the احداث tab was dropped for the 3-tab frame,
/// D-598 — an event-typed session shows only under الكل). Int on the wire
/// (mirrors `SIMF.Common.Enums.SessionType`; the enum itself is frozen D-219);
/// [fromJson] is tolerant (int OR name); a null / absent / unknown value
/// decodes to null (an untyped session shows only under the "الكل / All" tab).
enum SessionType {
  workshop(0, 'Workshop'),
  session(1, 'Session'),
  event(2, 'Event');

  const SessionType(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  static SessionType? fromJson(Object? value) {
    if (value is String) {
      for (final t in values) {
        if (t.wireName == value) {
          return t;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final t in values) {
        if (t.wireValue == asInt) {
          return t;
        }
      }
    }
    return null;
  }
}

/// One speaker card carried on a cached session — mirrors
/// `SIMF.Contracts.Programme.PublicSessionSpeaker`. The country **flag** is
/// rendered from [countryId] (the names are the label/fallback) and the
/// **avatar** from [photoRelativePath]; all four are nullable + append-only
/// (D-271). Decoded here so the cached programme feeds the session detail
/// (Page_017) with no extra fetch — the Sessions **list** row itself does not
/// render speakers (the mockup row is time/index/title/description, Page_016
/// Design).
@immutable
class SessionSpeaker {
  const SessionSpeaker({
    required this.id,
    required this.name,
    required this.nameArabic,
    required this.displayOrder,
    required this.role,
    this.title,
    this.titleArabic,
    this.countryId,
    this.countryNameEn,
    this.countryNameAr,
    this.photoRelativePath,
  });

  factory SessionSpeaker.fromJson(Map<String, dynamic> json) => SessionSpeaker(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        title: json['title'] as String?,
        titleArabic: json['titleArabic'] as String?,
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
        role: SessionSpeakerRole.fromJson(json['role']),
        countryId: (json['countryId'] as num?)?.toInt(),
        countryNameEn: json['countryNameEn'] as String?,
        countryNameAr: json['countryNameAr'] as String?,
        photoRelativePath: json['photoRelativePath'] as String?,
      );

  final String id;
  final String name;
  final String nameArabic;
  final String? title;
  final String? titleArabic;
  final int displayOrder;
  final SessionSpeakerRole role;
  final int? countryId;
  final String? countryNameEn;
  final String? countryNameAr;
  final String? photoRelativePath;

  String localizedName({required bool isArabic}) =>
      _pickRequired(nameArabic, name, isArabic);

  // 2026-07-19 (owner) — the speaker's rank/title in the active locale
  // (Arabic ↔ English), matching how the name localizes. Null when unset.
  String? localizedTitle({required bool isArabic}) =>
      _pickOptional(titleArabic, title, isArabic);

  String? localizedCountry({required bool isArabic}) =>
      _pickOptional(countryNameAr, countryNameEn, isArabic);
}

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

  /// The session's time-phase (upcoming / live / ended) against [nowUtc].
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
      _pickRequired(titleArabic, title, isArabic);

  String? localizedHall({required bool isArabic}) =>
      _pickOptional(hallNameArabic, hallName, isArabic);

  String? localizedDescription({required bool isArabic}) =>
      _pickOptional(descriptionArabic, description, isArabic);

  String? localizedCategory({required bool isArabic}) =>
      _pickOptional(categoryNameArabic, categoryName, isArabic);
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

/// D-452 (Figma 883:2308 "تفاصيل اليوم") — one programme day: a calendar date
/// with its own bilingual title, a [hasImage] flag (the `ProgrammeDayImage`
/// asset, served by the anonymous route), and the day's sessions. From
/// `GET /app/programme/days` (`PublicProgrammeDays = { days: [...] }`).
@immutable
class ProgrammeDay {
  const ProgrammeDay({
    required this.id,
    required this.date,
    required this.title,
    required this.titleArabic,
    required this.displayOrder,
    required this.hasImage,
    required this.sessions,
  });

  factory ProgrammeDay.fromJson(Map<String, dynamic> json) => ProgrammeDay(
        id: json['id'] as String? ?? '',
        date: _parseDate(json['date']),
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
        hasImage: json['hasImage'] as bool? ?? false,
        sessions: (json['sessions'] as List? ?? const <dynamic>[])
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => SessionListItem.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false),
      );

  final String id;

  /// The day's calendar date (local midnight — the wire sends a date-only).
  final DateTime date;
  final String title;
  final String titleArabic;
  final int displayOrder;

  /// True when a logo/banner is uploaded for this day (drives the banner image
  /// vs the fallback).
  final bool hasImage;
  final List<SessionListItem> sessions;

  String localizedTitle({required bool isArabic}) =>
      _pickRequired(titleArabic, title, isArabic);
}

/// The envelope for the day-grouped programme (`PublicProgrammeDays`).
@immutable
class ProgrammeDaysPage {
  const ProgrammeDaysPage(this.days);

  factory ProgrammeDaysPage.fromJson(Object? data) {
    final list =
        (data is Map ? data['days'] : null) as List? ?? const <dynamic>[];
    final days = list
        .whereType<Map<dynamic, dynamic>>()
        .map((e) => ProgrammeDay.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false);
    return ProgrammeDaysPage(days);
  }

  final List<ProgrammeDay> days;
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

  /// FR-702 (owner 2026-07-31) — the free-text notice the Control Panel authors

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

  /// [arrivalGraceMinutes] as a Duration, for comparing against a start time.
  Duration get arrivalGrace => Duration(minutes: arrivalGraceMinutes);

  DateTime get startLocal => saudiOf(start);
  DateTime get endLocal => saudiOf(end);

  String localizedTitle({required bool isArabic}) =>
      _pickRequired(titleArabic, title, isArabic);

  String localizedHall({required bool isArabic}) =>
      _pickRequired(hallNameArabic, hallName, isArabic);

  String? localizedDescription({required bool isArabic}) =>
      _pickOptional(descriptionArabic, description, isArabic);

  String? localizedCategory({required bool isArabic}) =>
      _pickOptional(categoryNameArabic, categoryName, isArabic);
}

/// The caller's own active seat for a session — the `myCell` cell of
/// `SIMF.Contracts.Sessions.SessionSeatMap` (`GET /app/sessions/{id}/seats`,
/// approved account). The Page_017 card shows `الصف {rowLabel} · مقعد
/// {seatNumber}`
/// — there is **no column axis** (Page_017 L-3.1). The full grid + the cell
/// `kind`
/// belong to the My-Seat screen (18).
@immutable
class MySeat {
  const MySeat({
    required this.reservationId,
    required this.rowLabel,
    required this.seatNumber,
  });

  final String reservationId;
  final String rowLabel;
  final int seatNumber;

  /// Reads `myCell` from a `SessionSeatMap` payload; null when the caller has
  /// no
  /// active reservation (the card is hidden — Page_017 L-3).
  static MySeat? fromSeatMap(Object? data) {
    final cell = (data is Map ? data['myCell'] : null);
    if (cell is! Map) {
      return null;
    }
    final map = cell.cast<String, dynamic>();
    return MySeat(
      reservationId: map['reservationId'] as String? ?? '',
      rowLabel: map['rowLabel'] as String? ?? '',
      seatNumber: (map['seatNumber'] as num?)?.toInt() ?? 0,
    );
  }
}

/// The two top filter pills (Page_016): Upcoming (still to come) and Forum (the
/// whole programme). Filtering is **client-side** over the cached list (L-1).
enum SessionsView { upcoming, forum }

/// Pure client-side filter over the cached programme (Page_016 L-1): [view]
/// (Upcoming = `start >= nowUtc`, L-2), an optional [localDay] (matched on
/// the session's device-local calendar day), and a free-text [query] over
/// title/description/code (both languages). Input order is preserved (the
/// server returns the list time-ordered, L-5).
List<SessionListItem> filterSessions(
  List<SessionListItem> items, {
  required SessionsView view,
  required DateTime nowUtc,
  DateTime? localDay,
  String query = '',
}) {
  return items.where((session) {
    if (view == SessionsView.upcoming && session.start.isBefore(nowUtc)) {
      return false;
    }
    if (localDay != null && !sameLocalDay(session.startLocal, localDay)) {
      return false;
    }
    return sessionMatchesQuery(session, query);
  }).toList(growable: false);
}

/// Whether [session] matches a free-text [query] across its title, description
/// and code in BOTH languages, case-insensitively. An empty query matches
/// everything.
///
/// One definition, because there were two. This predicate was also written
/// inline in `sessions_screen.dart`, and only the copy inside [filterSessions]
/// had tests — while only the inline one actually ran, because
/// [filterSessions] had no production caller at all. Five tests were passing
/// over a rule nobody shipped.
bool sessionMatchesQuery(SessionListItem session, String query) {
  final needle = query.trim().toLowerCase();
  if (needle.isEmpty) {
    return true;
  }
  final haystack = <String?>[
    session.title,
    session.titleArabic,
    session.description,
    session.descriptionArabic,
    session.code,
  ].whereType<String>().join(' ').toLowerCase();
  return haystack.contains(needle);
}

/// The sessions of [day] the programme screen shows, given the active type tab
/// and the search box. Input order is preserved (the server returns the day
/// time-ordered).
///
/// Lifted out of the screen's `build()`, which re-derived it on every keystroke
/// and every rebuild (section 2: no data shaping in build).
List<SessionListItem> sessionsForDay(
  ProgrammeDay day, {
  SessionType? type,
  String query = '',
}) => <SessionListItem>[
  for (final session in day.sessions)
    if (type == null || session.type == type)
      if (sessionMatchesQuery(session, query)) session,
];

/// The distinct device-local calendar days present in [items], ascending — the
/// data-driven day strip (Page_016: the event's "remaining days"). Each entry
/// is
/// a midnight-local [DateTime].
List<DateTime> sessionDays(List<SessionListItem> items) =>
    distinctLocalDays(items, (session) => session.startLocal);

/// Decodes a `speakers[]` array (shared by the list item + the detail). A
/// missing / null array decodes to an empty list — never null on the wire
/// (L-7).
List<SessionSpeaker> _decodeSpeakers(Object? data) =>
    (data as List? ?? const <dynamic>[])
        .whereType<Map<dynamic, dynamic>>()
        .map((e) => SessionSpeaker.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false);

/// Parses a date-only wire value (`yyyy-MM-dd`, the .NET `DateOnly`
/// serialisation) into a local-midnight [DateTime] for the day strip / banner.
DateTime _parseDate(Object? value) {
  if (value is String && value.isNotEmpty) {
    final parsed = parseWireOrNull(value);
    if (parsed != null) {
      return DateTime(parsed.year, parsed.month, parsed.day);
    }
  }
  return DateTime.fromMillisecondsSinceEpoch(0);
}

/// Picks the locale value of a required bilingual pair, falling back to the
/// other language when one side is blank.
String _pickRequired(String arabic, String english, bool isArabic) {
  final ar = arabic.trim();
  final en = english.trim();
  return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
}

/// Picks the locale value of an optional bilingual pair; null when both blank.
String? _pickOptional(String? arabic, String? english, bool isArabic) {
  final ar = arabic?.trim() ?? '';
  final en = english?.trim() ?? '';
  final value =
      isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  return value.isEmpty ? null : value;
}

/// P5.1 / D-241 — the caller's own attendance state for one session.
/// [arrived] is true while an open attendance row exists (entered, not yet
/// left). [enter] / [leave] are stored absolute instants — render them through
/// [formatSaudiTime12], never `toLocal()` (D-770).
class HallAttendanceStatus {
  const HallAttendanceStatus({
    required this.arrived,
    this.enter,
    this.leave,
    this.method,
  });

  factory HallAttendanceStatus.fromJson(Map<String, dynamic> json) {
    return HallAttendanceStatus(
      arrived: json['arrived'] as bool? ?? false,
      enter: json['enter'] == null
          ? null
          : parseWireDateTime(json['enter'], 'enter'),
      leave: json['leave'] == null
          ? null
          : parseWireDateTime(json['leave'], 'leave'),
      method: HallAttendanceMethod.fromJson(json['method']),
    );
  }

  final bool arrived;
  final DateTime? enter;
  final DateTime? leave;
  final HallAttendanceMethod? method;
}

/// How the attendee's presence in the hall was recorded — an operator scanning
/// the badge QR at the door, or the attendee's own device crossing the GPS
/// geofence. Int on the wire (mirrors `SIMF.Common.Enums.AttendanceMethod`);
/// [fromJson] is tolerant (int OR name) like the app's other server enums, and
/// a null / absent / unknown value decodes to null rather than throwing.
enum HallAttendanceMethod {
  qrScan(0, 'QrScan'),
  geofence(1, 'Geofence');

  const HallAttendanceMethod(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  static HallAttendanceMethod? fromJson(Object? value) {
    if (value is String) {
      for (final m in values) {
        if (m.wireName == value) {
          return m;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final m in values) {
        if (m.wireValue == asInt) {
          return m;
        }
      }
    }
    return null;
  }
}
