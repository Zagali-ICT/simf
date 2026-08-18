import 'package:flutter/foundation.dart';
import 'package:simf_app/core/utils/bilingual.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';

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
      pickLocalized(titleArabic, title, isArabic: isArabic);
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
