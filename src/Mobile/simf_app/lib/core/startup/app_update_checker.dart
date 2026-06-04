import 'package:flutter_riverpod/flutter_riverpod.dart';

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

/// The launch-time store-update check (Page_001 Logic L-2).
///
/// The latest-version lookup is **store-native** (Play Store / App Store
/// in-app-update APIs), never a SIMF endpoint — there is no SIMF version API,
/// and one must not be added. This abstraction is the seam the splash boots
/// against; the concrete store-plugin implementation is wired at
/// store-submission time by overriding [appUpdateCheckerProvider].
///
/// Until then the app ships [NoopAppUpdateChecker], which never blocks boot —
/// the correct behaviour pre-launch, when there is no published build to
/// compare the installed version against.
abstract class AppUpdateChecker {
  /// Queries the native store for the update status. Implementations must
  /// never throw — an unreachable store resolves to [AppUpdateStatus.upToDate]
  /// so a failed check never strands the user on the splash (Logic L-6).
  Future<AppUpdateStatus> check();

  /// Opens the native store listing so the user can update. A no-op until a
  /// concrete checker is wired.
  Future<void> openStoreListing();
}

/// The pre-launch default: always reports up-to-date and never opens a store.
class NoopAppUpdateChecker implements AppUpdateChecker {
  const NoopAppUpdateChecker();

  @override
  Future<AppUpdateStatus> check() async => AppUpdateStatus.upToDate;

  @override
  Future<void> openStoreListing() async {}
}

/// The active update checker. Overridden with a store-plugin-backed
/// implementation at store-submission time (Page_001 Logic L-2). The default
/// is the safe no-op so development and pre-launch builds boot unhindered.
final appUpdateCheckerProvider = Provider<AppUpdateChecker>((ref) {
  return const NoopAppUpdateChecker();
});
