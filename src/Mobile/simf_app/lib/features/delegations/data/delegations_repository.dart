import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'delegation_models.dart';

/// Data layer for the Delegations screen — App "الوفود" (Figma 1426:10771). One
/// read of the public invited-country delegations (`GET /app/delegations`,
/// anonymous). Throws [ApiFailure] on a wire error — the screen maps it to the
/// error + retry surface.
class DelegationsRepository {
  DelegationsRepository(this._client);

  final SimfApiClient _client;

  Future<Delegations> getDelegations() {
    return _client.get<Delegations>(
      '/app/delegations',
      decodeData: Delegations.fromData,
    );
  }

  /// Bi-Meeting rework — `GET /app/countries/{countryId}/available-slots`
  /// (approved-only) → the target delegation's free meeting slots, derived from
  /// its availability windows. A failure is treated by the sheet as "no slots"
  /// (the request can still be sent subject-only).
  Future<List<DelegationSlot>> getAvailableSlots(int countryId) {
    return _client.get<List<DelegationSlot>>(
      '/app/countries/$countryId/available-slots',
      decodeData: (data) => (data as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => DelegationSlot.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false),
    );
  }

  /// Bi-Meeting rework — `POST /app/delegation-meeting-requests` (approved-only,
  /// delegate-gated). Body `{ targetCountryCode, attendeeCount, subject }` plus an
  /// optional picked slot. The response is discarded (success is enough). The
  /// screens map 403 (not a delegate) / 400 (target not invited) / 409 (duplicate).
  Future<void> submitMeetingRequest({
    required String targetCountryCode,
    required int attendeeCount,
    required String subject,
    DateTime? slotStartUtc,
    DateTime? slotEndUtc,
  }) {
    return _client.post<bool>(
      '/app/delegation-meeting-requests',
      body: <String, dynamic>{
        'targetCountryCode': targetCountryCode,
        'attendeeCount': attendeeCount,
        'subject': subject,
        if (slotStartUtc != null)
          'slotStartUtc': slotStartUtc.toUtc().toIso8601String(),
        if (slotEndUtc != null)
          'slotEndUtc': slotEndUtc.toUtc().toIso8601String(),
      },
      decodeData: (_) => true,
    );
  }

  /// Bi-Meeting rework — `POST /app/delegation-meeting-requests/{id}/confirm`
  /// (approved-only; the caller must be an eligible member of the TARGET
  /// delegation). Confirms an Approved (awaiting) meeting; returns the meeting
  /// summary shown on the confirm screen (no requester PII — stripped server-side).
  /// Maps 403 (not the other party) / 409 (not awaiting confirmation).
  Future<DelegationMeetingSummary> confirmMeeting(String requestId) {
    return _client.post<DelegationMeetingSummary>(
      '/app/delegation-meeting-requests/$requestId/confirm',
      body: const <String, dynamic>{},
      decodeData: (data) => DelegationMeetingSummary.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }
}

/// Bi-Meeting rework — one bookable meeting slot offered by a delegation
/// (parity with `SpeakerSlot`).
class DelegationSlot {
  const DelegationSlot({required this.startUtc, required this.endUtc});

  factory DelegationSlot.fromJson(Map<String, dynamic> json) => DelegationSlot(
        startUtc: DateTime.parse(json['startUtc'] as String).toUtc(),
        endUtc: DateTime.parse(json['endUtc'] as String).toUtc(),
      );

  final DateTime startUtc;
  final DateTime endUtc;
}

/// Bi-Meeting rework — the delegation-meeting summary returned by the confirm
/// endpoint, shown on the confirm screen. Carries no requester PII.
class DelegationMeetingSummary {
  const DelegationMeetingSummary({
    required this.requestingCountry,
    required this.targetCountry,
    required this.subject,
    this.slotStartUtc,
  });

  factory DelegationMeetingSummary.fromJson(Map<String, dynamic> json) =>
      DelegationMeetingSummary(
        requestingCountry: (json['requestingCountry'] as String?) ?? '',
        targetCountry: (json['targetCountry'] as String?) ?? '',
        subject: (json['subject'] as String?) ?? '',
        slotStartUtc: json['slotStartUtc'] == null
            ? null
            : DateTime.tryParse(json['slotStartUtc'] as String)?.toUtc(),
      );

  final String requestingCountry;
  final String targetCountry;
  final String subject;
  final DateTime? slotStartUtc;
}

final delegationsRepositoryProvider = Provider<DelegationsRepository>((ref) {
  return DelegationsRepository(ref.watch(simfApiClientProvider));
});

/// The invited-country delegations (auto-disposed so it re-reads each visit).
final delegationsProvider =
    FutureProvider.autoDispose<Delegations>((ref) async {
  return ref.watch(delegationsRepositoryProvider).getDelegations();
});
