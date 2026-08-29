import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/core/utils/image_upload_mime.dart';
import 'package:simf_app/features/staff/data/staff_endpoints.dart';
import 'package:simf_app/features/staff/data/staff_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-509 — data layer for the staff walk-in registration (Figma 1467:12357).
///
/// Authority: the endpoints require the JWT **`Visitors.RegisterOnsite`**
/// permission (the same capability the CP walk-in desk uses) — a staff app user
/// without that grant gets 403. Creating a walk-in lands a **PendingApproval**
/// visitor; the optional ID-document + avatar are attached afterwards by user
/// id.
class StaffRepository {
  StaffRepository(this._client);

  final SimfApiClient _client;

  /// `POST /app/staff/visitors/register-onsite` → the created (pending) visitor.
  /// Throws [ApiFailure] on a wire/validation error (the screen surfaces the
  /// server's bilingual message on a 400).
  Future<StaffWalkInResult> registerVisitor(StaffWalkInRequest request) {
    return _client.post<StaffWalkInResult>(
      StaffEndpoints.registerOnsite,
      body: request.toJson(),
      decodeData: (data) => StaffWalkInResult.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }

  /// `GET /app/staff/walk-in-mode` — which fields this desk must demand.
  Future<StaffWalkInMode> walkInMode() {
    return _client.get<StaffWalkInMode>(
      StaffEndpoints.walkInMode,
      decodeData: (data) => StaffWalkInMode.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }

  /// `POST /app/staff/visitors/{userId}/id-document` (multipart) — attaches the
  /// new visitor's ID-document image. 5 MB / jpeg|png|webp + human-face gate are
  /// server-side. Returns true on success.
  Future<bool> uploadIdImage({
    required String userId,
    required List<int> bytes,
    required String filename,
  }) {
    return _client.upload<bool>(
      StaffEndpoints.visitorIdDocument(userId),
      bytes: bytes,
      filename: filename,
      contentType: imageUploadMime(filename),
      decodeData: (_) => true,
    );
  }

  /// `POST /app/staff/visitors/{userId}/avatar` (multipart) — attaches the new
  /// visitor's profile photo. 2 MB / jpeg|png|webp are server-side.
  Future<bool> uploadAvatar({
    required String userId,
    required List<int> bytes,
    required String filename,
  }) {
    return _client.upload<bool>(
      StaffEndpoints.visitorAvatar(userId),
      bytes: bytes,
      filename: filename,
      contentType: imageUploadMime(filename),
      decodeData: (_) => true,
    );
  }
}

final staffRepositoryProvider = Provider<StaffRepository>((ref) {
  return StaffRepository(ref.watch(simfApiClientProvider));
});
