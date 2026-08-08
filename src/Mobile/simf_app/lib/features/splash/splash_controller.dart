import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/route_names.dart';
import '../../core/motion/motion_durations.dart';
import '../../core/organization_profile/organization_profile.dart';
import '../../core/startup/app_update_checker.dart';

/// Minimum time the logo is shown so the splash never flickers (Page_001 Logic
/// L-1). Boot work that finishes sooner waits for this. Exposed as a provider
/// so widget/unit tests can collapse it to [Duration.zero].
final minSplashDurationProvider = Provider<Duration>((ref) {
  return MotionDurations.splashHold;
});

/// What the splash should do once boot work resolves.
sealed class SplashState {
  const SplashState();
}

/// Boot work is still running — show the logo (+ optional spinner).
class SplashLoading extends SplashState {
  const SplashLoading();
}

/// A mandatory store update gates entry (Page_001 Logic L-2). Non-dismissible:
/// the only path forward is the store listing.
class SplashUpdateRequired extends SplashState {
  const SplashUpdateRequired();
}

/// Boot resolved — route out (Page_001 Logic L-5). Exactly one of [routeName]
/// (a [RouteNames] value → `goNamed`) or [location] (a resumed path → `go`) is
/// set. When [softUpdate] is true the widget shows a dismissible update prompt
/// first, then continues to the destination.
class SplashReady extends SplashState {
  const SplashReady({this.routeName, this.location, this.softUpdate = false})
      : assert(
          routeName != null || location != null,
          'A ready splash must carry a route name or a location.',
        );

  final String? routeName;
  final String? location;
  final bool softUpdate;
}

/// Orchestrates the launch sequence behind the splash logo (Page_001 Logic
/// L-1..L-6): runs the version-policy update check (D-736) and the
/// minimum-display timer concurrently, waits for the auth cold-start restore
/// to resolve, then emits the route-out decision. The widget reacts to the
/// emitted [SplashState].
class SplashController extends Notifier<SplashState> {
  @override
  SplashState build() {
    // Touch the auth controller so its cold-start restore is running.
    ref.read(authControllerProvider);
    unawaited(_run());
    return const SplashLoading();
  }

  Future<void> _run() async {
    // D-495 — load the edition-generic forum config at the splash: the controller
    // hydrates instantly from local storage, then this refreshes it from the API
    // (Last-Modified / 304). Available app-wide afterwards via orgProfileProvider.
    // Fire-and-forget — never blocks boot.
    unawaited(ref.read(orgProfileProvider.notifier).warm());

    final checker = ref.read(appUpdateCheckerProvider);
    final minDisplay = ref.read(minSplashDurationProvider);

    final results = await Future.wait(<Future<Object?>>[
      Future<void>.delayed(minDisplay),
      // Hard cap (Logic L-6): a hung policy check never blocks boot.
      checker.check().timeout(
        const Duration(seconds: 5),
        onTimeout: () => AppUpdateStatus.upToDate,
      ),
    ]);
    final updateStatus = results[1] as AppUpdateStatus;

    if (updateStatus == AppUpdateStatus.forced) {
      state = const SplashUpdateRequired();
      return;
    }

    await _waitForAuthResolved();

    state = _readyFor(softUpdate: updateStatus == AppUpdateStatus.optional);
  }

  /// Completes once the auth state machine has left its [AuthStateInitial]
  /// cold-start phase (Page_001 Logic L-4).
  Future<void> _waitForAuthResolved() async {
    if (ref.read(authControllerProvider) is! AuthStateInitial) {
      return;
    }
    final completer = Completer<void>();
    final sub = ref.listen<AuthState>(
      authControllerProvider,
      (_, next) {
        if (next is! AuthStateInitial && !completer.isCompleted) {
          completer.complete();
        }
      },
    );
    try {
      // Hard cap (Logic L-6): never wait forever on the cold-start restore; on
      // timeout, fall through and route out on whatever state is current.
      await completer.future.timeout(TimeoutPolicy.splashStartup);
    } on TimeoutException {
      // Intentionally swallowed — the splash advances rather than hanging.
    } finally {
      sub.close();
    }
  }

  /// The route-out decision (Page_001 Logic L-5).
  SplashReady _readyFor({required bool softUpdate}) {
    final auth = ref.read(authControllerProvider);
    final prefs = ref.read(simfPrefsStorageProvider);

    if (auth is AuthStateSignedIn) {
      // D-374 — the add-profile-first gate also applies to the cold-start
      // restore (same rule as routeAfterAuth after sign-in / the OTP step);
      // the restore re-hydrates /users/me, so the flag is fresh.
      if (!auth.session.user.profileComplete) {
        return SplashReady(
          routeName: RouteNames.signUpVisitor,
          softUpdate: softUpdate,
        );
      }
      // Always open Home after launch — the app no longer resumes to the last
      // screen (D-431, owner request). Cold-starting into the last visited
      // screen could strand the user (e.g. re-opening the camera scanner with a
      // stale error); the bottom-nav shell still keeps in-session tab state.
      return SplashReady(routeName: RouteNames.home, softUpdate: softUpdate);
    }

    if (auth is AuthStateAwaitingOtp) {
      return SplashReady(
        routeName: RouteNames.verifyOtp,
        softUpdate: softUpdate,
      );
    }

    // Signed out: first run → onboarding; otherwise the sign-in entry.
    final onboarded = prefs.getBool(StorageKeys.onboardingCompleted) ?? false;
    return SplashReady(
      routeName: onboarded ? RouteNames.signIn : RouteNames.onboarding,
      softUpdate: softUpdate,
    );
  }
}

final splashControllerProvider =
    NotifierProvider<SplashController, SplashState>(SplashController.new);
