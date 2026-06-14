import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'profile_models.dart';

/// App-local data layer for the visitor profile (Page_007): the upsert + the
/// four lookups, over the authenticated `/app/account/*` + `/app/organisations`
/// endpoints. Throws [ApiFailure] (from `simf_data_pkg`) on a wire error; the
/// screen surfaces `error.message`.
class ProfileRepository {
  ProfileRepository(this._client);

  final SimfApiClient _client;

  /// E1 — pre-fill (empty on a first-time profile).
  Future<UserProfileResponse> getMyProfile() {
    return _client.get<UserProfileResponse>(
      '/app/account/user-profile',
      decodeData: (data) =>
          UserProfileResponse.fromJson(_asMap(data)),
    );
  }

  /// E2 — upsert (the Save). Returns the upserted profile.
  Future<UserProfileResponse> upsertMyProfile(
    UpsertUserProfileRequest request,
  ) {
    return _client.post<UserProfileResponse>(
      '/app/account/user-profile',
      body: request.toJson(),
      decodeData: (data) =>
          UserProfileResponse.fromJson(_asMap(data)),
    );
  }

  /// E3 — nationality lookup.
  Future<List<CountryItem>> getCountries() {
    return _client.get<List<CountryItem>>(
      '/app/account/user-profile/countries',
      decodeData: (data) => _keyed(data, 'countries', CountryItem.fromJson),
    );
  }

  /// E4 — profile-type lookup (active, non-Admin rows). [isVisitor] mirrors the
  /// نوع التسجيل chip: true → audience rows, false → partner/Other rows, null → all.
  Future<List<ProfileTypeItem>> getProfileTypes({bool? isVisitor}) {
    return _client.get<List<ProfileTypeItem>>(
      '/app/account/profile-types',
      queryParameters: isVisitor == null
          ? null
          : <String, dynamic>{'isVisitor': isVisitor},
      decodeData: (data) => _keyed(data, 'items', ProfileTypeItem.fromJson),
    );
  }

  /// E5 — interests lookup (active rows, ordered).
  Future<List<InterestItem>> getInterests() {
    return _client.get<List<InterestItem>>(
      '/app/account/interests',
      decodeData: (data) => _keyed(data, 'interests', InterestItem.fromJson),
    );
  }

  /// E6 — organisation typeahead. `search` over AR/EN name (null → top rows).
  Future<List<OrganisationItem>> searchOrganisations({
    String? search,
    int top = 20,
  }) {
    final query = <String, dynamic>{'top': top};
    if (search != null && search.trim().isNotEmpty) {
      query['search'] = search.trim();
    }
    return _client.get<List<OrganisationItem>>(
      '/app/organisations',
      queryParameters: query,
      // E6 returns a bare JSON array (ApiResult<IReadOnlyList<...>>).
      decodeData: (data) => (data as List<dynamic>? ?? const <dynamic>[])
          .map((e) => OrganisationItem.fromJson(_asMap(e)))
          .toList(),
    );
  }

  /// Self-service ID-image upload (multipart) — `POST /app/account/user-profile/id-image`.
  /// 5 MB / jpeg|png|webp guards are server-side (content-type + magic-byte
  /// verified), so the MIME is derived from [filename] and sent on the file part.
  /// Returns true on success.
  Future<bool> uploadIdImage({
    required List<int> bytes,
    required String filename,
  }) {
    return _client.upload<bool>(
      '/app/account/user-profile/id-image',
      bytes: bytes,
      filename: filename,
      contentType: mimeForFilename(filename),
      decodeData: (data) => data is bool ? data : true,
    );
  }

  /// Maps a filename extension to the MIME the server's gate accepts
  /// (jpeg / png / webp). Null for an unknown extension — the picker only yields
  /// these three, so a null would be a programming error, not a user path.
  @visibleForTesting
  static String? mimeForFilename(String filename) {
    final lower = filename.toLowerCase();
    if (lower.endsWith('.jpg') || lower.endsWith('.jpeg')) {
      return 'image/jpeg';
    }
    if (lower.endsWith('.png')) {
      return 'image/png';
    }
    if (lower.endsWith('.webp')) {
      return 'image/webp';
    }
    return null;
  }

  static Map<String, dynamic> _asMap(Object? data) =>
      (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};

  /// Decode `{ key: [ {...}, ... ] }` → a typed list.
  static List<T> _keyed<T>(
    Object? data,
    String key,
    T Function(Map<String, dynamic>) fromJson,
  ) {
    final rows = _asMap(data)[key] as List<dynamic>? ?? const <dynamic>[];
    return rows.map((e) => fromJson(_asMap(e))).toList();
  }
}

final profileRepositoryProvider = Provider<ProfileRepository>((ref) {
  return ProfileRepository(ref.watch(simfApiClientProvider));
});

/// Best-effort reference number (`SIMF-2026-…`) for the badge + profile ID line.
/// It lives on the user-profile (the My-Area dashboard doesn't carry it), so the
/// badge/profile read it here. Null while loading / on error.
final referenceNumberProvider = FutureProvider.autoDispose<String?>((ref) async {
  try {
    final profile = await ref.watch(profileRepositoryProvider).getMyProfile();
    final ref0 = profile.referenceNumber?.trim();
    return (ref0 == null || ref0.isEmpty) ? null : ref0;
  } on ApiFailure {
    return null;
  }
});
