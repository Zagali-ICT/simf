import 'package:flutter/foundation.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// D-500 (Wave 5, الطلبات 1408:9726) — which kind a unified "My requests" row is.
/// Mirrors the `AppRequestKind` contract (int on the wire). The app renders the
/// type headline from this value.
enum AppRequestKind {
  speakerMeeting,
  delegationMeeting,
  sessionAttendance,
  participationDocument,
  badgeUpdate;

  static AppRequestKind fromIndex(int? index) {
    switch (index) {
      case 1:
        return AppRequestKind.delegationMeeting;
      case 2:
        return AppRequestKind.sessionAttendance;
      case 3:
        return AppRequestKind.participationDocument;
      case 4:
        return AppRequestKind.badgeUpdate;
      default:
        return AppRequestKind.speakerMeeting;
    }
  }

  /// The wire int the cancel endpoint expects.
  int get wireValue => index;
}

/// The unified display status — mirrors `MeetingRequestStatus`
/// (Pending=0 / Accepted=1 / Rejected=2 / Cancelled=3). Unknown falls back to
/// [pending].
enum AppRequestStatus {
  pending,
  accepted,
  rejected,
  cancelled;

  static AppRequestStatus fromIndex(int? index) {
    switch (index) {
      case 1:
        return AppRequestStatus.accepted;
      case 2:
        return AppRequestStatus.rejected;
      case 3:
        return AppRequestStatus.cancelled;
      default:
        return AppRequestStatus.pending;
    }
  }
}

/// One row on the الطلبات screen — mirrors the `AppRequestItem` contract.
@immutable
class AppRequestItem {
  const AppRequestItem({
    required this.kind,
    required this.id,
    required this.title,
    required this.titleArabic,
    required this.status,
    required this.createdAt,
    required this.canCancel,
    this.eventDate,
    this.subtitle,
    this.subtitleArabic,
    this.speakerId,
    this.countryId,
    this.responseNote,
    this.checkedIn = false,
  });

  factory AppRequestItem.fromJson(Map<String, dynamic> json) {
    final eventRaw = json['eventDate'] as String?;
    final createdRaw = json['createdAt'] as String?;
    return AppRequestItem(
      kind: AppRequestKind.fromIndex(json['kind'] as int?),
      id: json['id'] as String? ?? '',
      title: json['title'] as String? ?? '',
      titleArabic: json['titleArabic'] as String? ?? '',
      status: AppRequestStatus.fromIndex(json['status'] as int?),
      eventDate: eventRaw == null ? null : parseWireOrNull(eventRaw),
      createdAt: (createdRaw == null ? null : parseWireOrNull(createdRaw)) ??
          DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
      canCancel: json['canCancel'] as bool? ?? false,
      subtitle: (json['subtitle'] as String?)?.trim().isEmpty ?? true
          ? null
          : (json['subtitle'] as String).trim(),
      subtitleArabic:
          (json['subtitleArabic'] as String?)?.trim().isEmpty ?? true
              ? null
              : (json['subtitleArabic'] as String).trim(),
      speakerId: (json['speakerId'] as String?)?.trim().isEmpty ?? true
          ? null
          : (json['speakerId'] as String).trim(),
      countryId: (json['countryId'] as num?)?.toInt(),
      responseNote: (json['responseNote'] as String?)?.trim().isEmpty ?? true
          ? null
          : (json['responseNote'] as String).trim(),
      checkedIn: json['checkedIn'] as bool? ?? false,
    );
  }

  final AppRequestKind kind;
  final String id;
  final String title;
  final String titleArabic;
  final AppRequestStatus status;
  final DateTime? eventDate;
  final DateTime createdAt;
  final bool canCancel;

  /// D-590 — optional secondary descriptor under the name on the المقابلات card
  /// (Figma 1701:9406). Carries the speaker's rank for a speaker meeting; null
  /// for the other kinds, where the card falls back to the meeting-type line.
  final String? subtitle;

  /// 2026-07-19 (owner) — the Arabic twin of [subtitle] (the speaker's rank), so
  /// the rank line localizes AR/EN. Null for the non-speaker kinds / when unset.
  final String? subtitleArabic;

  /// D-745 — the speaker's id for a speaker meeting, so the bilateral-meetings
  /// card renders the speaker photo from the public asset route
  /// (`/app/assets/SpeakerPhoto/{id}/image`). Null for the other kinds.
  final String? speakerId;

  /// D-745 — the ISO 3166-1 numeric country id for the meeting card's flag: the
  /// speaker's nationality (speaker meeting) or the target country (delegation).
  /// Null when unset / for the non-meeting kinds.
  final int? countryId;

  /// R-3 — the admin's response note for a decided request (e.g. the rejection
  /// reason). Null when none. Append-only wire field.
  final String? responseNote;

  /// QA B12 — true once an operator checked the meeting in at the hall. The
  /// server still folds its internal `Done` state onto `accepted` for [status]
  /// (the shipped wire contract is 0-3), so this append-only flag is the only
  /// way the requester can tell "confirmed" from "attended". False otherwise.
  final bool checkedIn;

  /// The context line under the type headline, in the active locale (AR/EN with
  /// a fallback).
  String localizedSubtitle({required bool isArabic}) {
    final ar = titleArabic.trim();
    final en = title.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  /// D-590 — the speaker's rank (the المقابلات subtitle line) in the active
  /// locale (owner 2026-07-19). Null for the non-speaker kinds / when unset.
  String? localizedRank({required bool isArabic}) {
    final ar = subtitleArabic?.trim() ?? '';
    final en = subtitle?.trim() ?? '';
    final picked =
        isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
    return picked.isEmpty ? null : picked;
  }

  /// The date shown on the card — the session/meeting slot, else the submit date.
  DateTime get displayDate => eventDate ?? createdAt;

  /// D-745 — the two bilateral-meeting kinds shown on the اللقاءات الثنائية page
  /// (speaker + delegation); the other kinds live only on the requests history.
  bool get isMeetingKind =>
      kind == AppRequestKind.speakerMeeting ||
      kind == AppRequestKind.delegationMeeting;

  /// Reads the bare `[ ... ]` list the endpoint returns.
  static List<AppRequestItem> listFromData(Object? data) =>
      (data as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => AppRequestItem.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}

/// The participation-document kinds the app can request — mirrors
/// `ParticipationDocumentType` (int on the wire).
/// The status chip that should actually be active, given what is on screen.
///
/// A selected status whose chip has dropped to zero items — the user cancelled
/// their only pending request, say — falls back to "All", so the screen never
/// strands them on a chip-less "no results" view. Returns null for "All".
AppRequestStatus? effectiveRequestFilter(
  List<AppRequestItem> items,
  AppRequestStatus? selected,
) =>
    (selected != null && items.any((item) => item.status == selected))
        ? selected
        : null;

/// The rows to show for [selected], applying [effectiveRequestFilter] first.
List<AppRequestItem> filterRequests(
  List<AppRequestItem> items,
  AppRequestStatus? selected,
) {
  final active = effectiveRequestFilter(items, selected);
  if (active == null) {
    return items;
  }
  return items.where((item) => item.status == active).toList(growable: false);
}

enum ParticipationDocumentType {
  attendanceCertificate,
  participationLetter,
  invitationLetter;

  int get wireValue => index;
}
