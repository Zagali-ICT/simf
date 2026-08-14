import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/account/data/account_endpoints.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

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
      AccountEndpoints.userProfile,
      decodeData: (data) =>
          UserProfileResponse.fromJson(_asMap(data)),
    );
  }

  /// E2 — upsert (the Save). Returns the upserted profile.
  Future<UserProfileResponse> upsertMyProfile(
    UpsertUserProfileRequest request,
  ) {
    return _client.post<UserProfileResponse>(
      AccountEndpoints.userProfile,
      body: request.toJson(),
      decodeData: (data) =>
          UserProfileResponse.fromJson(_asMap(data)),
    );
  }

  /// E3 — nationality lookup.
  Future<List<CountryItem>> getCountries() {
    return _client.get<List<CountryItem>>(
      AccountEndpoints.countries,
      decodeData: (data) => _keyed(data, 'countries', CountryItem.fromJson),
    );
  }

  /// E4 — profile-type lookup (active, non-Admin rows). [isVisitor] mirrors the
  /// نوع التسجيل chip: true → audience rows, false → partner/Other rows, null → all.
  Future<List<ProfileTypeItem>> getProfileTypes({bool? isVisitor}) {
    return _client.get<List<ProfileTypeItem>>(
      AccountEndpoints.profileTypes,
      queryParameters: isVisitor == null
          ? null
          : <String, dynamic>{'isVisitor': isVisitor},
      decodeData: (data) => _keyed(data, 'items', ProfileTypeItem.fromJson),
    );
  }

  /// E5 — interests lookup (active rows, ordered).
  Future<List<InterestItem>> getInterests() {
    return _client.get<List<InterestItem>>(
      AccountEndpoints.interests,
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
      AccountEndpoints.organisations,
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
      AccountEndpoints.idImage,
      bytes: bytes,
      filename: filename,
      contentType: mimeForFilename(filename),
      decodeData: (data) => data is bool ? data : true,
    );
  }

  /// Self-service face-photo upload (multipart) — `POST /app/account/avatar`.
  /// The face photo IS the profile avatar; it is captured live and posted here
  /// during sign-up (mandatory for men, optional for women) so the top profile
  /// photo shows the real face. 2 MB / jpeg|png|webp guards are server-side.
  /// Returns true on success.
  Future<bool> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) {
    return _client.upload<bool>(
      AccountEndpoints.avatar,
      bytes: bytes,
      filename: filename,
      contentType: mimeForFilename(filename),
      decodeData: (_) => true,
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

/// Bi-Meeting rework (D-760) — the signed-in user's two admin-assigned
/// meeting-eligibility flags (speaker / delegation). They REPLACE the VIP
/// tier as the Bi-Meeting gate, so eligibility no longer follows the
/// audience tier: the gates read [MeetingAccess.speaker] /
/// [MeetingAccess.delegation] / [MeetingAccess.any], and one profile read
/// serves both flags. [MeetingAccess.none] for guests / on error.
///
/// D-731 — the speaker profile is a **Guest+ browse** surface, and the
/// profile GET behind this sits on the shared per-IP "auth" rate-limit
/// bucket (sign-in / OTP / sign-up). So this provider (a) short-circuits
/// guests with NO network call — a guest is never eligible — and (b) is
/// **not** autoDispose, so the flags are cached across speaker-profile opens
/// (fetched once, NOT re-fetched on every open); browsing many speakers
/// therefore can't drain the auth budget for co-located attendees behind one
/// venue IP. It watches the auth controller, so it re-fetches on any auth
/// transition (sign-in/out, token refresh/reload, or the D-563 stamp-roll +
/// token revoke that follows an admin account-type change) — the cache
/// tracks live eligibility and never goes stale.
final currentUserMeetingAccessProvider =
    FutureProvider<MeetingAccess>((ref) async {
  final auth = ref.watch(authControllerProvider);
  if (auth is! AuthStateSignedIn) {
    return MeetingAccess.none;
  }
  try {
    final profile = await ref.watch(profileRepositoryProvider).getMyProfile();
    return MeetingAccess(
      speaker: profile.allowsSpeakerMeeting,
      delegation: profile.allowsDelegationMeeting,
    );
  } on ApiFailure {
    return MeetingAccess.none;
  }
});

/// The signed-in user's Bi-Meeting entitlements (admin-assigned per-user flags).
class MeetingAccess {
  const MeetingAccess({required this.speaker, required this.delegation});

  /// May request a **speaker** meeting.
  final bool speaker;

  /// May request a **delegation** (وفد) meeting.
  final bool delegation;

  /// Entitled to at least one meeting type (gates the Bi-Meeting page / home tile).
  bool get any => speaker || delegation;

  static const MeetingAccess none =
      MeetingAccess(speaker: false, delegation: false);
}

/// Cache-buster for the signed-in user's avatar. Bumped after a successful
/// avatar upload so [myAvatarBytesProvider] refetches and every avatar on
/// screen (home greeting / badge / profile) shows the new photo immediately —
/// the avatar URL is stable, so a version token is the only thing that forces a
/// refetch (the server also caches it `max-age=300`).
final avatarBustProvider = StateProvider<int>((ref) => 0);

/// The signed-in user's avatar image bytes, fetched **through the authenticated
/// API client** (which attaches the bearer + `X-App-Key` and handles the
/// self-signed TLS — none of which `Image.network` can do; the avatar endpoint
/// is bearer-gated, so a bare network image silently 401s / fails the cert).
///
/// Returns null when the user is signed out, has no avatar (the server streams
/// 404), or on any failure — the caller then falls back to the brand mark.
/// Re-runs whenever [avatarBustProvider] changes (after an upload). Skips the
/// fetch entirely for a user with no known avatar who hasn't uploaded this
/// session, so a photo-less account makes no avatar request.
final myAvatarBytesProvider =
    FutureProvider.autoDispose<Uint8List?>((ref) async {
  final auth = ref.watch(authControllerProvider);
  if (auth is! AuthStateSignedIn) {
    return null;
  }
  final bust = ref.watch(avatarBustProvider);
  final user = auth.session.user;
  final hasServerAvatar = (user.avatarUrl ?? '').trim().isNotEmpty;
  if (!hasServerAvatar && bust == 0) {
    return null;
  }
  try {
    return await ref
        .watch(simfApiClientProvider)
        .getBytes(AccountEndpoints.avatarOf(user.id));
  } on ApiFailure {
    return null;
  }
});
