import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:pub_semver/pub_semver.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../net/core_endpoints.dart';
import 'app_update_checker.dart';

/// D-736 — the server app-update policy (`GET /app/version-policy`): the
/// per-platform minimum supported version (older installs are hard-blocked),
/// latest released version (older installs get a dismissible prompt) and the
/// store listing URL the Update button opens. Every field is null when the
/// admin has not configured it — a null turns that rule off (fail-open).
class PlatformVersionPolicy {
  const PlatformVersionPolicy({
    this.minVersion,
    this.latestVersion,
    this.storeUrl,
  });

  final String? minVersion;
  final String? latestVersion;
  final String? storeUrl;

  static PlatformVersionPolicy fromJson(Map<String, dynamic> json) =>
      PlatformVersionPolicy(
        minVersion: json['minVersion'] as String?,
        latestVersion: json['latestVersion'] as String?,
        storeUrl: json['storeUrl'] as String?,
      );
}

/// Both platforms' policies in one payload — the app picks its own side.
class AppVersionPolicy {
  const AppVersionPolicy({required this.android, required this.ios});

  final PlatformVersionPolicy android;
  final PlatformVersionPolicy ios;

  /// The policy for the platform this build runs on (iOS vs Android). Web
  /// never reaches here — the checker no-ops on `kIsWeb`.
  PlatformVersionPolicy get currentPlatform =>
      defaultTargetPlatform == TargetPlatform.iOS ? ios : android;

  static AppVersionPolicy fromJson(Map<String, dynamic> json) =>
      AppVersionPolicy(
        android: PlatformVersionPolicy.fromJson(_asStringMap(json['android'])),
        ios: PlatformVersionPolicy.fromJson(_asStringMap(json['ios'])),
      );

  static Map<String, dynamic> _asStringMap(dynamic value) =>
      (value as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
}

class AppVersionPolicyRepository {
  AppVersionPolicyRepository(this._client);

  final SimfApiClient _client;

  Future<AppVersionPolicy> fetch() => _client.get<AppVersionPolicy>(
        CoreEndpoints.versionPolicy,
        decodeData: (data) => AppVersionPolicy.fromJson(
          (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
        ),
      );
}

final appVersionPolicyRepositoryProvider =
    Provider<AppVersionPolicyRepository>((ref) {
  return AppVersionPolicyRepository(ref.watch(simfApiClientProvider));
});

/// The real installed app version (pubspec `version`), resolved once in
/// `main()` via `PackageInfo.fromPlatform()` and overridden here. Empty in
/// tests unless overridden — screens render a dash and the update check fails
/// open.
final installedAppVersionProvider = Provider<String>((ref) => '');

/// Lenient semver parse: trims, tolerates a leading `v`, null on anything
/// unparseable — an admin typo must disable the rule, never crash or block.
///
/// Build metadata is **dropped** (SemVer 2.0.0 §10 — it is not part of
/// precedence). `pub_semver` orders it, so without this a `minVersion` of
/// `"1.0.0+42"` (the natural Flutter `pubspec` form) would outrank an installed
/// `"1.0.0"` and force the user forever — the published store build's version
/// name is also `"1.0.0"`, so updating could never clear the gate (a brick).
Version? tryParseVersion(String? raw) {
  final trimmed = raw?.trim() ?? '';
  if (trimmed.isEmpty) {
    return null;
  }
  final normalized =
      trimmed.startsWith('v') || trimmed.startsWith('V')
          ? trimmed.substring(1)
          : trimmed;
  try {
    final parsed = Version.parse(normalized);
    if (parsed.build.isEmpty) {
      return parsed;
    }
    return Version(
      parsed.major,
      parsed.minor,
      parsed.patch,
      pre: parsed.preRelease.isEmpty ? null : parsed.preRelease.join('.'),
    );
  } on FormatException {
    return null;
  }
}

/// The store URL is only usable when it is an absolute http(s) link (the
/// server sanitises too — this is defence-in-depth, and the anti-brick rule:
/// no valid Update target means no gate and no prompt, never a dead end).
String? usableStoreUrl(String? raw) {
  final trimmed = raw?.trim() ?? '';
  if (trimmed.isEmpty) {
    return null;
  }
  final uri = Uri.tryParse(trimmed);
  return uri != null && (uri.scheme == 'http' || uri.scheme == 'https')
      ? trimmed
      : null;
}

/// The pure version-policy decision (D-736), shared by the launch checker and
/// the About-screen manual check. Snoozing is deliberately NOT applied here —
/// the manual check must ignore it.
///
/// Fail-open by construction: an unparseable installed version, an unset or
/// unparseable policy knob, or a missing store URL each resolve toward
/// [AppUpdateStatus.upToDate] — a misconfigured policy can never block the app
/// without a working Update button.
AppUpdateStatus evaluateVersionPolicy({
  required String installedVersion,
  required PlatformVersionPolicy policy,
}) {
  final installed = tryParseVersion(installedVersion);
  if (installed == null || usableStoreUrl(policy.storeUrl) == null) {
    return AppUpdateStatus.upToDate;
  }
  final min = tryParseVersion(policy.minVersion);
  if (min != null && installed < min) {
    return AppUpdateStatus.forced;
  }
  final latest = tryParseVersion(policy.latestVersion);
  if (latest != null && installed < latest) {
    return AppUpdateStatus.optional;
  }
  return AppUpdateStatus.upToDate;
}
