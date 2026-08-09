import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/core/external_link.dart';
import 'package:simf_app/core/startup/app_update_checker.dart';
import 'package:simf_app/core/startup/app_version_policy.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';
import 'package:url_launcher/url_launcher.dart';

/// D-736 — the production [AppUpdateChecker]: compares the installed version
/// against the admin-configured server policy (`GET /app/version-policy`).
///
/// Fail-open everywhere (Page_001 Logic L-6): ANY error — server unreachable,
/// timeout, malformed payload, unparseable versions, even an unoverridden data
/// config in a test harness — resolves to [AppUpdateStatus.upToDate] so a
/// failed check never blocks boot. A hard block additionally requires a live,
/// successful fetch on THIS launch (never a cached policy) plus a usable store
/// URL, so users can never be stranded on a dead-end update screen.
class ServerAppUpdateChecker implements AppUpdateChecker {
  ServerAppUpdateChecker(this._ref, {DateTime Function()? now})
      : _now = now ?? DateTime.now;

  /// How long a dismissed optional update stays quiet for the same version.
  static const Duration snoozeWindow = Duration(days: 3);

  final Ref _ref;
  final DateTime Function() _now;

  /// The validated store URL from the last successful check — the Update
  /// button's target for both the forced and the optional splash dialog.
  String? _storeUrl;

  /// The latest version last offered as an optional update; what a snooze
  /// records so only that exact version stays quiet.
  String? _offeredVersion;

  @override
  Future<AppUpdateStatus> check() async {
    try {
      final policy =
          await _ref.read(appVersionPolicyRepositoryProvider).fetch();
      final platform = policy.currentPlatform;
      _storeUrl = usableStoreUrl(platform.storeUrl);

      final status = evaluateVersionPolicy(
        installedVersion: _ref.read(installedAppVersionProvider),
        policy: platform,
      );
      if (status == AppUpdateStatus.optional) {
        _offeredVersion = platform.latestVersion?.trim();
        if (_isSnoozed(_offeredVersion)) {
          return AppUpdateStatus.upToDate;
        }
      }
      return status;
    } on Object {
      // Fail-open (Logic L-6): an unreachable/broken policy never blocks boot.
      return AppUpdateStatus.upToDate;
    }
  }

  @override
  Future<void> snoozeOptionalUpdate() async {
    final offered = _offeredVersion;
    if (offered == null || offered.isEmpty) {
      return;
    }
    final prefs = _ref.read(simfPrefsStorageProvider);
    await prefs.setString(StorageKeys.appUpdateSnoozedVersion, offered);
    await prefs.setString(
      StorageKeys.appUpdateSnoozedAtIso,
      // Zone-free, on the project's own clock. This wrote toUtc() until
      // 2026-08-01; the value is a device-local snooze marker that is never
      // sent or displayed, so a second clock bought nothing and meant a second
      // time
      // source existed alongside saudiNow().
      formatWire(_now()),
    );
  }

  @override
  Future<void> openStoreListing() async {
    final url = _storeUrl;
    if (url == null) {
      return;
    }
    await launchExternalUri(
      Uri.parse(url),
      mode: LaunchMode.externalApplication,
    );
  }

  bool _isSnoozed(String? offered) {
    if (offered == null || offered.isEmpty) {
      return false;
    }
    final prefs = _ref.read(simfPrefsStorageProvider);
    if (prefs.getString(StorageKeys.appUpdateSnoozedVersion) != offered) {
      return false;
    }
    // parseWireOrNull, not DateTime.tryParse: a marker written by a build
    // before this change carries a trailing Z, and a raw parse would leave it
    // zone-bearing so the comparison below would skew by the device's offset.
    final snoozedAt = parseWireOrNull(
      prefs.getString(StorageKeys.appUpdateSnoozedAtIso),
    );
    return snoozedAt != null &&
        _now().difference(snoozedAt) < snoozeWindow;
  }
}
