import 'package:flutter/foundation.dart';

import '../../../core/utils/saudi_time.dart';
import '../../sessions/data/session_models.dart' show SessionStatus;

/// One card on the "my sessions" list — App "تفاصيل الجلسات" (Figma 1388:9067),
/// mirroring `SIMF.Contracts.Account.MyAreaSessionItem`. The card shows the
/// title, the day/time line + category chip, and the primary speaker
/// ([speakerNameEn] / [speakerNameAr] + [speakerTitle] rank) with the hall.
/// Bilingual fields are paired (the UI picks per locale); [attended] and
/// [isFavourite] are the two per-user flags.
@immutable
class MyAreaSessionItem {
  const MyAreaSessionItem({
    required this.id,
    required this.title,
    required this.titleArabic,
    required this.start,
    required this.end,
    required this.status,
    required this.attended,
    required this.isFavourite,
    this.hallNameEn,
    this.hallNameAr,
    this.categoryNameEn,
    this.categoryNameAr,
    this.speakerNameEn,
    this.speakerNameAr,
    this.speakerTitle,
  });

  final String id;
  final String title;
  final String titleArabic;
  final DateTime start;
  final DateTime end;
  final SessionStatus status;
  final bool attended;
  final bool isFavourite;
  final String? hallNameEn;
  final String? hallNameAr;
  final String? categoryNameEn;
  final String? categoryNameAr;
  final String? speakerNameEn;
  final String? speakerNameAr;
  final String? speakerTitle;

  /// The session's start on the Saudi event-local wall clock (wire value UTC).
  DateTime get startLocal => saudiOf(start);

  /// The session length in whole minutes (floored at 0).
  int get durationMinutes {
    final minutes = end.difference(start).inMinutes;
    return minutes < 0 ? 0 : minutes;
  }

  /// True once the session has finished (its end is in the past).
  bool hasEnded(DateTime nowUtc) => end.isBefore(nowUtc);

  /// True while the session is still to come (its start is in the future).
  bool isUpcoming(DateTime nowUtc) => start.isAfter(nowUtc);

  /// True when the session has a replayable recording on the archive tab
  /// (broadcast lifecycle reached Recorded / Published).
  bool get isArchived =>
      status == SessionStatus.recorded || status == SessionStatus.published;

  String localizedTitle(bool isArabic) =>
      _pickRequired(titleArabic, title, isArabic);

  String? localizedHall(bool isArabic) =>
      _pickOptional(hallNameAr, hallNameEn, isArabic);

  String? localizedCategory(bool isArabic) =>
      _pickOptional(categoryNameAr, categoryNameEn, isArabic);

  String? localizedSpeaker(bool isArabic) =>
      _pickOptional(speakerNameAr, speakerNameEn, isArabic);

  static MyAreaSessionItem fromJson(Map<String, dynamic> json) =>
      MyAreaSessionItem(
        id: json['id'] as String? ?? '',
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        start: parseWireUtc(json['start'], 'start'),
        end: parseWireUtc(json['end'], 'end'),
        status: SessionStatus.fromJson(json['status']),
        attended: json['attended'] as bool? ?? false,
        isFavourite: json['isFavourite'] as bool? ?? false,
        hallNameEn: json['hallNameEn'] as String?,
        hallNameAr: json['hallNameAr'] as String?,
        categoryNameEn: json['categoryNameEn'] as String?,
        categoryNameAr: json['categoryNameAr'] as String?,
        speakerNameEn: json['speakerNameEn'] as String?,
        speakerNameAr: json['speakerNameAr'] as String?,
        speakerTitle: json['speakerTitle'] as String?,
      );
}

/// The envelope for the "my sessions" list (`MyAreaSessions = { items: [...] }`).
@immutable
class MyAreaSessions {
  const MyAreaSessions(this.items);

  final List<MyAreaSessionItem> items;

  static MyAreaSessions fromData(Object? data) {
    final list = (data is Map ? data['items'] : null) as List? ??
        const <dynamic>[];
    final items = list
        .whereType<Map<dynamic, dynamic>>()
        .map((e) => MyAreaSessionItem.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false);
    return MyAreaSessions(items);
  }
}

String _pickRequired(String arabic, String english, bool isArabic) {
  final ar = arabic.trim();
  final en = english.trim();
  return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
}

String? _pickOptional(String? arabic, String? english, bool isArabic) {
  final ar = arabic?.trim() ?? '';
  final en = english?.trim() ?? '';
  final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  return value.isEmpty ? null : value;
}
