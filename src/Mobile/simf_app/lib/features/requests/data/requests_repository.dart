import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/requests/data/request_models.dart';
import 'package:simf_app/features/requests/data/requests_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-500 (Wave 5, الطلبات 1408:9726) — data layer for the unified requests feed:
/// the user's requests across all kinds (`GET /app/my-requests`), the two new
/// standalone submissions (`POST /app/document-requests`, `/app/badge-requests`),
/// and the self-cancel (`POST /app/my-requests/cancel`). All approved-only.
/// Throws [ApiFailure] on a wire error.
class RequestsRepository {
  RequestsRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/my-requests` → the user's requests, newest first.
  Future<List<AppRequestItem>> getMyRequests() {
    return _client.get<List<AppRequestItem>>(
      RequestsEndpoints.mine,
      decodeData: AppRequestItem.listFromData,
    );
  }

  /// `POST /app/document-requests` — submit a participation-document request.
  Future<void> submitDocumentRequest({
    required ParticipationDocumentType documentType,
    String? note,
  }) {
    return _client.post<bool>(
      RequestsEndpoints.documentRequests,
      body: <String, dynamic>{
        'documentType': documentType.wireValue,
        if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
      },
      decodeData: (_) => true,
    );
  }

  /// `POST /app/badge-requests` — submit a badge job-title update request.
  Future<void> submitBadgeRequest({
    required String requestedJobTitle,
    String? note,
  }) {
    return _client.post<bool>(
      RequestsEndpoints.badgeRequests,
      body: <String, dynamic>{
        'requestedJobTitle': requestedJobTitle.trim(),
        if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
      },
      decodeData: (_) => true,
    );
  }

  /// `POST /app/my-requests/cancel` — withdraw one of the user's own pending
  /// requests (speaker / document / badge).
  Future<void> cancelRequest({
    required AppRequestKind kind,
    required String id,
  }) {
    return _client.post<bool>(
      RequestsEndpoints.cancel,
      body: <String, dynamic>{'kind': kind.wireValue, 'id': id},
      decodeData: (_) => true,
    );
  }
}

final requestsRepositoryProvider = Provider<RequestsRepository>((ref) {
  return RequestsRepository(ref.watch(simfApiClientProvider));
});

/// The unified feed (auto-disposed; invalidate after a submit/cancel to refetch).
final myRequestsProvider =
    FutureProvider.autoDispose<List<AppRequestItem>>((ref) {
  return ref.watch(requestsRepositoryProvider).getMyRequests();
});
