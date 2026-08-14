import 'package:flutter/foundation.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// The My-Area (منطقتي) dashboard — mirrors `SIMF.Contracts.Account.MyAreaDashboard`
/// (`GET /app/account/dashboard`): the identity card, two counters, and today's
/// merged schedule. Read-only; the app never writes here (Page_014).
@immutable
class MyAreaDashboard {
  const MyAreaDashboard({
    required this.identity,
    required this.counters,
    required this.todaySchedule,
  });

  factory MyAreaDashboard.fromJson(Map<String, dynamic> json) => MyAreaDashboard(
        identity: MyAreaIdentity.fromJson(_map(json['identity'])),
        counters: MyAreaCounters.fromJson(_map(json['counters'])),
        todaySchedule: (json['todaySchedule'] as List? ?? const <dynamic>[])
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => MyAreaScheduleItem.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false),
      );

  final MyAreaIdentity identity;
  final MyAreaCounters counters;
  final List<MyAreaScheduleItem> todaySchedule;

  static Map<String, dynamic> _map(Object? v) =>
      (v as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
}

/// The identity card. `qrId` is null until the account is Approved (Page_014 L-1).
@immutable
class MyAreaIdentity {
  const MyAreaIdentity({
    required this.fullNameAr,
    required this.fullNameEn,
    this.qrId,
    this.avatarUrl,
    this.tierNameEn,
    this.tierNameAr,
    this.pageColor,
    this.isVisitor = true,
  });

  factory MyAreaIdentity.fromJson(Map<String, dynamic> json) => MyAreaIdentity(
        fullNameAr: json['fullNameAr'] as String? ?? '',
        fullNameEn: json['fullNameEn'] as String? ?? '',
        qrId: json['qrId'] as String?,
        avatarUrl: json['avatarUrl'] as String?,
        tierNameEn: json['tierNameEn'] as String?,
        tierNameAr: json['tierNameAr'] as String?,
        pageColor: json['pageColor'] as String?,
        isVisitor: json['isVisitor'] as bool? ?? true,
      );

  final String fullNameAr;
  final String fullNameEn;
  final String? qrId;
  final String? avatarUrl;
  final String? tierNameEn;
  final String? tierNameAr;
  final String? pageColor;

  /// True for audience profile types; false for partner/exhibitor ("Other")
  /// types — drives the QR-page actions (D-426). Defaults true.
  final bool isVisitor;

  /// Name for the active locale (Arabic primary, English fallback — L-8).
  String localizedName(bool isArabic) {
    final ar = fullNameAr.trim();
    final en = fullNameEn.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  /// Tier word for the active locale, or null when no ProfileType is assigned.
  String? localizedTier(bool isArabic) {
    final ar = tierNameAr?.trim() ?? '';
    final en = tierNameEn?.trim() ?? '';
    final v = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
    return v.isEmpty ? null : v;
  }
}

/// The two stat counters (Page_014 L-2/L-3).
@immutable
class MyAreaCounters {
  const MyAreaCounters({
    required this.bookedSessionsCount,
    required this.meetingsCount,
  });

  factory MyAreaCounters.fromJson(Map<String, dynamic> json) => MyAreaCounters(
        bookedSessionsCount: (json['bookedSessionsCount'] as num?)?.toInt() ?? 0,
        meetingsCount: (json['meetingsCount'] as num?)?.toInt() ?? 0,
      );

  final int bookedSessionsCount;
  final int meetingsCount;
}

/// One row of today's merged schedule (Page_014 L-4). `kind` is `"Session"` or
/// `"Meeting"`; a business meeting carries no title and uses its `subject`.
@immutable
class MyAreaScheduleItem {
  const MyAreaScheduleItem({
    required this.kind,
    required this.start,
    required this.titleEn, required this.titleAr, required this.status, this.end,
    this.hallNameEn,
    this.hallNameAr,
    this.subject,
    this.sessionId,
    this.meetingId,
  });

  factory MyAreaScheduleItem.fromJson(Map<String, dynamic> json) =>
      MyAreaScheduleItem(
        kind: json['kind'] as String? ?? '',
        start: parseWireOrNull(json['start'] as String? ?? '') ??
            DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
        end: parseWireOrNull(json['end'] as String? ?? ''),
        titleEn: json['titleEn'] as String? ?? '',
        titleAr: json['titleAr'] as String? ?? '',
        hallNameEn: json['hallNameEn'] as String?,
        hallNameAr: json['hallNameAr'] as String?,
        subject: json['subject'] as String?,
        status: json['status'] as String? ?? '',
        sessionId: json['sessionId'] as String?,
        meetingId: json['meetingId'] as String?,
      );

  final String kind;
  final DateTime start;
  final DateTime? end;
  final String titleEn;
  final String titleAr;
  final String? hallNameEn;
  final String? hallNameAr;
  final String? subject;
  final String status;
  final String? sessionId;
  final String? meetingId;

  bool get isSession => kind == 'Session';

  /// Title for the active locale; falls back to the meeting subject when the
  /// item carries no title (a business meeting).
  String localizedTitle(bool isArabic) {
    final ar = titleAr.trim();
    final en = titleEn.trim();
    final title = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
    if (title.isNotEmpty) {
      return title;
    }
    return subject?.trim() ?? '';
  }

  String? localizedHall(bool isArabic) {
    final ar = hallNameAr?.trim() ?? '';
    final en = hallNameEn?.trim() ?? '';
    final v = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
    return v.isEmpty ? null : v;
  }
}
