import 'package:flutter/foundation.dart';

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
    this.countryId,
    this.countryNameEn,
    this.countryNameAr,
    this.photoRelativePath,
  });

  final String id;
  final String name;
  final String nameArabic;
  final String? title;
  final int displayOrder;
  final SessionSpeakerRole role;
  final int? countryId;
  final String? countryNameEn;
  final String? countryNameAr;
  final String? photoRelativePath;

  String localizedName(bool isArabic) =>
      _pickRequired(nameArabic, name, isArabic);

  String? localizedCountry(bool isArabic) =>
      _pickOptional(countryNameAr, countryNameEn, isArabic);

  static SessionSpeaker fromJson(Map<String, dynamic> json) => SessionSpeaker(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        title: json['title'] as String?,
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
        role: SessionSpeakerRole.fromJson(json['role']),
        countryId: (json['countryId'] as num?)?.toInt(),
        countryNameEn: json['countryNameEn'] as String?,
        countryNameAr: json['countryNameAr'] as String?,
        photoRelativePath: json['photoRelativePath'] as String?,
      );
}

/// One row in the cached programme — mirrors
/// `SIMF.Contracts.Programme.PublicSessionListItem` (`GET /app/programme/sessions`).
/// [startUtc]/[endUtc] are UTC on the wire — the UI renders device-local
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
    required this.startUtc,
    required this.endUtc,
    required this.status,
    required this.speakers,
    this.description,
    this.descriptionArabic,
    this.categoryId,
    this.categoryName,
    this.categoryNameArabic,
    this.primaryThemeColor,
  });

  final String id;
  final String code;
  final String title;
  final String titleArabic;
  final String hallId;
  final String hallName;
  final String hallNameArabic;
  final DateTime startUtc;
  final DateTime endUtc;
  final SessionStatus status;
  final List<SessionSpeaker> speakers;
  final String? description;
  final String? descriptionArabic;
  final String? categoryId;
  final String? categoryName;
  final String? categoryNameArabic;
  final String? primaryThemeColor;

  /// The session's start in the device-local zone (the wire value is UTC).
  DateTime get startLocal => startUtc.toLocal();

  String localizedTitle(bool isArabic) =>
      _pickRequired(titleArabic, title, isArabic);

  String? localizedHall(bool isArabic) =>
      _pickOptional(hallNameArabic, hallName, isArabic);

  String? localizedDescription(bool isArabic) =>
      _pickOptional(descriptionArabic, description, isArabic);

  String? localizedCategory(bool isArabic) =>
      _pickOptional(categoryNameArabic, categoryName, isArabic);

  static SessionListItem fromJson(Map<String, dynamic> json) => SessionListItem(
        id: json['id'] as String? ?? '',
        code: json['code'] as String? ?? '',
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        hallId: json['hallId'] as String? ?? '',
        hallName: json['hallName'] as String? ?? '',
        hallNameArabic: json['hallNameArabic'] as String? ?? '',
        startUtc: _parseUtc(json['startUtc']),
        endUtc: _parseUtc(json['endUtc']),
        status: SessionStatus.fromJson(json['status']),
        speakers: _speakers(json['speakers']),
        description: json['description'] as String?,
        descriptionArabic: json['descriptionArabic'] as String?,
        categoryId: json['categoryId'] as String?,
        categoryName: json['categoryName'] as String?,
        categoryNameArabic: json['categoryNameArabic'] as String?,
        primaryThemeColor: json['primaryThemeColor'] as String?,
      );

  static List<SessionSpeaker> _speakers(Object? data) =>
      (data as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => SessionSpeaker.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}

/// The envelope for the cached programme (`PublicSessions = { items: [...] }`).
/// Kept as a tiny wrapper so the repository reads `items` from one place.
@immutable
class SessionsPage {
  const SessionsPage(this.items);

  final List<SessionListItem> items;

  static SessionsPage fromJson(Object? data) {
    final list = (data is Map ? data['items'] : null) as List? ??
        const <dynamic>[];
    final items = list
        .whereType<Map<dynamic, dynamic>>()
        .map((e) => SessionListItem.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false);
    return SessionsPage(items);
  }
}

/// The two top filter pills (Page_016): Upcoming (still to come) and Forum (the
/// whole programme). Filtering is **client-side** over the cached list (L-1).
enum SessionsView { upcoming, forum }

/// Pure client-side filter over the cached programme (Page_016 L-1): [view]
/// (Upcoming = `startUtc >= nowUtc`, L-2), an optional [localDay] (matched on
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
  final needle = query.trim().toLowerCase();
  return items.where((session) {
    if (view == SessionsView.upcoming && session.startUtc.isBefore(nowUtc)) {
      return false;
    }
    if (localDay != null && !_sameLocalDay(session.startLocal, localDay)) {
      return false;
    }
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
  }).toList(growable: false);
}

/// The distinct device-local calendar days present in [items], ascending — the
/// data-driven day strip (Page_016: the event's "remaining days"). Each entry is
/// a midnight-local [DateTime].
List<DateTime> sessionDays(List<SessionListItem> items) {
  final byKey = <String, DateTime>{};
  for (final session in items) {
    final local = session.startLocal;
    final key = '${local.year}-${local.month}-${local.day}';
    byKey.putIfAbsent(key, () => DateTime(local.year, local.month, local.day));
  }
  final days = byKey.values.toList()..sort();
  return days;
}

bool _sameLocalDay(DateTime a, DateTime b) =>
    a.year == b.year && a.month == b.month && a.day == b.day;

/// Parses an ISO-8601 wire timestamp into a UTC [DateTime] (the contract is
/// always UTC). A missing / unparseable value falls back to the epoch in UTC so
/// the model never holds a local-zone instant by accident.
DateTime _parseUtc(Object? value) {
  if (value is String && value.isNotEmpty) {
    final parsed = DateTime.tryParse(value);
    if (parsed != null) {
      return parsed.toUtc();
    }
  }
  return DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
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
  final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  return value.isEmpty ? null : value;
}
