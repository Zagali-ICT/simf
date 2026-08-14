import 'package:flutter/foundation.dart';
import 'package:simf_app/core/utils/bilingual.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// One downloadable session presentation — App "عروض الجلسات" (Figma
/// 1388:7621),
/// mirroring `SIMF.Contracts.Programme.PublicPresentationItem`. Each card shows
/// the session it belongs to (title bilingual + start, for the day tabs), the
/// presenting speaker (bilingual), and the file metadata; the bytes are fetched
/// from `GET /app/presentations/{id}/file` by the تحميل button.
@immutable
class PresentationItem {
  const PresentationItem({
    required this.id,
    required this.sessionId,
    required this.sessionTitle,
    required this.sessionTitleArabic,
    required this.sessionStart,
    required this.speakerName,
    required this.speakerNameArabic,
    required this.fileName,
    required this.contentType,
    required this.sizeBytes,
  });

  factory PresentationItem.fromJson(Map<String, dynamic> json) =>
      PresentationItem(
        id: json['id'] as String? ?? '',
        sessionId: json['sessionId'] as String? ?? '',
        sessionTitle: json['sessionTitle'] as String? ?? '',
        sessionTitleArabic: json['sessionTitleArabic'] as String? ?? '',
        sessionStart: parseWireDateTime(json['sessionStart'], 'sessionStart'),
        speakerName: json['speakerName'] as String? ?? '',
        speakerNameArabic: json['speakerNameArabic'] as String? ?? '',
        fileName: json['fileName'] as String? ?? '',
        contentType:
            json['contentType'] as String? ?? 'application/octet-stream',
        sizeBytes: (json['sizeBytes'] as num?)?.toInt() ?? 0,
      );

  final String id;
  final String sessionId;
  final String sessionTitle;
  final String sessionTitleArabic;
  final DateTime sessionStart;
  final String speakerName;
  final String speakerNameArabic;
  final String fileName;
  final String contentType;
  final int sizeBytes;

  /// The session's start on the Saudi event-local wall clock (zone-free on the
  /// wire).
  DateTime get sessionStartLocal => saudiOf(sessionStart);

  String localizedSessionTitle({required bool isArabic}) =>
      pickLocalized(sessionTitleArabic, sessionTitle, isArabic: isArabic);

  String? localizedSpeaker({required bool isArabic}) =>
      pickLocalizedOrNull(speakerNameArabic, speakerName, isArabic: isArabic);
}

/// The envelope for the public presentations list (`PublicPresentations`).
@immutable
class PresentationsPage {
  const PresentationsPage(this.items);

  factory PresentationsPage.fromData(Object? data) {
    final list =
        (data is Map ? data['items'] : null) as List? ?? const <dynamic>[];
    final items = list
        .whereType<Map<dynamic, dynamic>>()
        .map((e) => PresentationItem.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false);
    return PresentationsPage(items);
  }

  final List<PresentationItem> items;
}
