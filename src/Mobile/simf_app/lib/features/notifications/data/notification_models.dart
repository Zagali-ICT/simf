import 'package:flutter/foundation.dart';

/// The notification severity band — mirrors `SIMF.Common.Enums.NotificationSeverity`.
///
/// On the wire this is a **string** name (`Info` / `Success` / `Warning` /
/// `Error`), not an integer (D-110). Parse tolerantly: an unknown / missing
/// value falls back to [NotificationSeverity.info].
enum NotificationSeverity {
  info,
  success,
  warning,
  error;

  static NotificationSeverity fromName(String? name) {
    switch ((name ?? '').trim().toLowerCase()) {
      case 'success':
        return NotificationSeverity.success;
      case 'warning':
        return NotificationSeverity.warning;
      case 'error':
        return NotificationSeverity.error;
      default:
        return NotificationSeverity.info;
    }
  }
}

/// One notification — mirrors `NotificationDto`.
///
/// `kind` and `severity` are **string** names on the wire (D-110); the localized
/// title/body pick AR or EN with a fallback. `createdAt` / `readAt` are UTC
/// strings parsed with [DateTime.tryParse].
@immutable
class NotificationItem {
  const NotificationItem({
    required this.id,
    required this.kind,
    required this.title,
    required this.titleArabic,
    required this.body,
    required this.bodyArabic,
    required this.severity,
    required this.isRead,
    this.readAt,
    this.createdAt,
    this.relatedEntityType,
    this.relatedEntityId,
  });

  final String id;
  final String kind;
  final String title;
  final String titleArabic;
  final String body;
  final String bodyArabic;
  final NotificationSeverity severity;
  final bool isRead;
  final DateTime? readAt;
  final DateTime? createdAt;

  /// Optional deep-link target the tile can route to (e.g. "Session" + the
  /// session id for a "rate this session" prompt).
  final String? relatedEntityType;
  final String? relatedEntityId;

  String localizedTitle(bool isArabic) {
    final ar = titleArabic.trim();
    final en = title.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  String localizedBody(bool isArabic) {
    final ar = bodyArabic.trim();
    final en = body.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  /// A copy marked read — clears the unread dot optimistically after a
  /// mark-read without re-fetching the whole list.
  NotificationItem markedRead() => NotificationItem(
        id: id,
        kind: kind,
        title: title,
        titleArabic: titleArabic,
        body: body,
        bodyArabic: bodyArabic,
        severity: severity,
        isRead: true,
        readAt: readAt,
        createdAt: createdAt,
        relatedEntityType: relatedEntityType,
        relatedEntityId: relatedEntityId,
      );

  static NotificationItem fromJson(Map<String, dynamic> json) {
    final readAtRaw = json['readAt'] as String?;
    final createdAtRaw = json['createdAt'] as String?;
    return NotificationItem(
      id: json['id'] as String? ?? '',
      kind: json['kind'] as String? ?? '',
      title: json['title'] as String? ?? '',
      titleArabic: json['titleArabic'] as String? ?? '',
      body: json['body'] as String? ?? '',
      bodyArabic: json['bodyArabic'] as String? ?? '',
      severity: NotificationSeverity.fromName(json['severity'] as String?),
      isRead: json['isRead'] as bool? ?? false,
      readAt: readAtRaw == null ? null : DateTime.tryParse(readAtRaw)?.toUtc(),
      createdAt:
          createdAtRaw == null ? null : DateTime.tryParse(createdAtRaw)?.toUtc(),
      relatedEntityType: json['relatedEntityType'] as String?,
      relatedEntityId: json['relatedEntityId'] as String?,
    );
  }

  /// Reads the `GridPage<NotificationDto> = { items: [...] }` envelope into the
  /// notification list.
  static List<NotificationItem> listFromData(Object? data) =>
      ((data is Map ? data['items'] : null) as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => NotificationItem.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}
