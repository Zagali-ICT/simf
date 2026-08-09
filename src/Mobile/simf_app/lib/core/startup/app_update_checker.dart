import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/core/startup/server_app_update_checker.dart';

/// The outcome of the launch-time app-version check (Page_001 Logic L-2).
enum AppUpdateStatus {
  /// The installed build is current — boot continues silently.
  upToDate,

  /// A newer build exists but the update is optional — the splash shows a
  /// dismissible prompt, then continues.
  optional,

  /// A newer build is mandatory — the splash shows a non-dismissible prompt
  /// and does not route into the app until the user updates.
  forced,
}

/// The launch-time update check (Page_001 Logic L-2, reshaped by D-736).
///
/// D-736 — the owner reversed the original store-native contract: the check
/// now reads the **SIMF server policy** (`GET /app/version-policy` — the
/// per-platform minimum/latest version + store URL an admin edits on the CP
/// configuration page) and compares it against the installed version. The
/// Update action deep-links to the configured store listing. This seam is what
/// the splash boots against; [ServerAppUpdateChecker] is the production
/// implementation.
abstract class AppUpdateChecker {
  /// Resolves the update status from the server policy. Implementations must
  /// never throw — an unreachable server resolves to
  /// [AppUpdateStatus.upToDate] so a failed check never strands the user on
  /// the splash (fail-open, Logic L-6).
  Future<AppUpdateStatus> check();

  /// Records the dismissal of an optional-update prompt so the same version
  /// stays quiet for [ServerAppUpdateChecker.snoozeWindow] (D-736). The
  /// About-screen manual check deliberately bypasses this.
  Future<void> snoozeOptionalUpdate();

  /// Opens the store listing configured for this platform. A no-op when the
  /// policy carries no usable store URL.
  Future<void> openStoreListing();
}

/// The inert default for web builds and tests: always up-to-date, never opens
/// a store, records nothing. (Web is a dev-diagnostics target only —
/// SIMF-MAA-001 ships Android + iOS.)
class NoopAppUpdateChecker implements AppUpdateChecker {
  const NoopAppUpdateChecker();

  @override
  Future<AppUpdateStatus> check() async => AppUpdateStatus.upToDate;

  @override
  Future<void> snoozeOptionalUpdate() async {}

  @override
  Future<void> openStoreListing() async {}
}

/// The active update checker: the server-policy implementation on device
/// (D-736), the safe no-op on web. Tests override this with fakes.
final appUpdateCheckerProvider = Provider<AppUpdateChecker>((ref) {
  if (kIsWeb) {
    return const NoopAppUpdateChecker();
  }
  return ServerAppUpdateChecker(ref);
});
