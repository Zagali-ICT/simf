import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-479 (#11 follow-up) — data layer for the read-only "My meetings" screen:
/// the meetings the signed-in user requested (speaker + delegation). One read,
/// `GET /app/my-meetings` (approved-only). Throws [ApiFailure] on a wire error.
class MyMeetingsRepository {
  MyMeetingsRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/my-meetings` → the user's meetings, newest first.
  Future<List<MyMeetingItem>> getMyMeetings() {
    return _client.get<List<MyMeetingItem>>(
      '/app/my-meetings',
      decodeData: MyMeetingItem.listFromData,
    );
  }
}

/// Which kind of meeting a row is — mirrors `MyMeetingKind` (int on the wire).
enum MyMeetingKind {
  speaker,
  delegation;

  static MyMeetingKind fromIndex(int? index) {
    switch (index) {
      case 1:
        return MyMeetingKind.delegation;
      default:
        return MyMeetingKind.speaker;
    }
  }
}

/// The request status — mirrors `MeetingRequestStatus` (Pending=0/Accepted=1/
/// Rejected=2, int on the wire). Unknown / missing falls back to [pending].
enum MyMeetingStatus {
  pending,
  accepted,
  rejected;

  static MyMeetingStatus fromIndex(int? index) {
    switch (index) {
      case 1:
        return MyMeetingStatus.accepted;
      case 2:
        return MyMeetingStatus.rejected;
      default:
        return MyMeetingStatus.pending;
    }
  }
}

/// One "My meetings" row — mirrors the `MyMeetingItem` contract.
@immutable
class MyMeetingItem {
  const MyMeetingItem({
    required this.kind,
    required this.id,
    required this.title,
    required this.titleArabic,
    required this.subject,
    required this.status,
    required this.createdAt,
    this.slotStartUtc,
  });

  final MyMeetingKind kind;
  final String id;
  final String title;
  final String titleArabic;
  final String subject;
  final MyMeetingStatus status;
  final DateTime? slotStartUtc;
  final DateTime createdAt;

  /// The counterparty name in the active locale (AR/EN with a fallback).
  String localizedTitle(bool isArabic) {
    final ar = titleArabic.trim();
    final en = title.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  static MyMeetingItem fromJson(Map<String, dynamic> json) {
    final slotRaw = json['slotStartUtc'] as String?;
    final createdRaw = json['createdAt'] as String?;
    return MyMeetingItem(
      kind: MyMeetingKind.fromIndex(json['kind'] as int?),
      id: json['id'] as String? ?? '',
      title: json['title'] as String? ?? '',
      titleArabic: json['titleArabic'] as String? ?? '',
      subject: json['subject'] as String? ?? '',
      status: MyMeetingStatus.fromIndex(json['status'] as int?),
      slotStartUtc:
          slotRaw == null ? null : DateTime.tryParse(slotRaw)?.toUtc(),
      createdAt:
          (createdRaw == null ? null : DateTime.tryParse(createdRaw)?.toUtc()) ??
              DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
    );
  }

  /// Reads the bare `[ ... ]` list the endpoint returns.
  static List<MyMeetingItem> listFromData(Object? data) =>
      (data as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => MyMeetingItem.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}

final myMeetingsRepositoryProvider = Provider<MyMeetingsRepository>((ref) {
  return MyMeetingsRepository(ref.watch(simfApiClientProvider));
});
