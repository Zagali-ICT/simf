import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/delegations/data/delegations_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Data layer for the Delegations screen — App "الوفود" (Figma 1426:10771). One
/// read of the public invited-country delegations (`GET /app/delegations`,
/// anonymous). Throws [ApiFailure] on a wire error — the screen maps it to the
/// error + retry surface.
class DelegationsRepository {
  DelegationsRepository(this._client);

  final SimfApiClient _client;

  Future<Delegations> getDelegations() {
    return _client.get<Delegations>(
      DelegationsEndpoints.list,
      decodeData: Delegations.fromData,
    );
  }

  /// Bi-Meeting rework — `GET /app/countries/{countryId}/available-slots`
  /// (approved-only) → the target delegation's free meeting slots, derived from
  /// its availability windows. A failure is treated by the sheet as "no slots"
  /// (the request can still be sent subject-only).
  Future<List<DelegationSlot>> getAvailableSlots(int countryId) {
    return _client.get<List<DelegationSlot>>(
      DelegationsEndpoints.availableSlots(countryId),
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
    DateTime? slotStart,
    DateTime? slotEnd,
  }) {
    return _client.post<bool>(
      DelegationsEndpoints.meetingRequests,
      body: <String, dynamic>{
        'targetCountryCode': targetCountryCode,
        'attendeeCount': attendeeCount,
        'subject': subject,
        if (slotStart != null)
          'slotStart': formatWire(slotStart),
        if (slotEnd != null)
          'slotEnd': formatWire(slotEnd),
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
      DelegationsEndpoints.confirmMeeting(requestId),
      body: const <String, dynamic>{},
      decodeData: (data) => DelegationMeetingSummary.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }

  /// B8 — `POST /app/delegation-meeting-requests/{id}/decline`, the exact
  /// mirror of [confirmMeeting]. An eligible member of the TARGET delegation
  /// may decline an Approved (awaiting) meeting instead of waiting for an
  /// admin to cancel it. Returns the same summary shape (no requester PII).
  /// Maps 403 (not the other party) / 409 (not awaiting confirmation).
  Future<DelegationMeetingSummary> declineMeeting(String requestId) {
    return _client.post<DelegationMeetingSummary>(
      DelegationsEndpoints.declineMeeting(requestId),
      body: const <String, dynamic>{},
      decodeData: (data) => DelegationMeetingSummary.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }
}

final delegationsRepositoryProvider = Provider<DelegationsRepository>((ref) {
  return DelegationsRepository(ref.watch(simfApiClientProvider));
});

/// The invited-country delegations (auto-disposed so it re-reads each visit).
final delegationsProvider =
    FutureProvider.autoDispose<Delegations>((ref) async {
  return ref.watch(delegationsRepositoryProvider).getDelegations();
});
