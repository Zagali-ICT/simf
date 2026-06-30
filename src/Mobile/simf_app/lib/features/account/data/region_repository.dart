import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// App-local data layer for the region lookup (D-547): the single public read of
/// the official Saudi administrative regions, over the authenticated
/// `GET /app/regions` endpoint (sign-in only). Throws [ApiFailure] (from
/// `simf_data_pkg`) on a wire error — the picker catches it and falls back to the
/// const `saudiRegions` list.
///
/// Kept SEPARATE from [ProfileRepository] (the place-of-birth picker is the only
/// consumer) to keep the blast radius small (D-545: repo + DTO in
/// `features/<f>/data`, the repo calls the client, never `dio` directly).
class RegionRepository {
  RegionRepository(this._client);

  final SimfApiClient _client;

  /// The active regions, ordered by SortOrder (server-side), for the picker.
  Future<List<RegionItem>> getRegions() {
    return _client.get<List<RegionItem>>(
      '/app/regions',
      // The endpoint returns a bare JSON array (ApiResult<IReadOnlyList<...>>),
      // mirroring ProfileRepository.searchOrganisations.
      decodeData: (data) => (data as List<dynamic>? ?? const <dynamic>[])
          .map((e) => RegionItem.fromJson(_asMap(e)))
          .toList(),
    );
  }

  static Map<String, dynamic> _asMap(Object? data) =>
      (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
}

/// Region picker row — `GET /app/regions` (D-547). [name] (English) is nullable;
/// [nameArabic] is always present. Wire keys: `code`, `name`, `nameArabic`.
@immutable
class RegionItem {
  const RegionItem({
    required this.code,
    required this.name,
    required this.nameArabic,
  });

  final String code;
  final String? name;
  final String nameArabic;

  static RegionItem fromJson(Map<String, dynamic> json) => RegionItem(
        code: json['code'] as String? ?? '',
        name: json['name'] as String?,
        nameArabic: json['nameArabic'] as String? ?? '',
      );
}

final regionRepositoryProvider = Provider<RegionRepository>((ref) {
  return RegionRepository(ref.watch(simfApiClientProvider));
});

/// The active regions for the place-of-birth picker. The picker FALLS BACK to
/// the const `saudiRegions` list on loading or error, so this provider is
/// allowed to fail without breaking the form (D-547 owner decision: keep the
/// hardcoded list as an offline fallback).
final regionsProvider = FutureProvider.autoDispose<List<RegionItem>>((ref) {
  return ref.watch(regionRepositoryProvider).getRegions();
});
