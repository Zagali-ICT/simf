import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_logo.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/core/startup/app_update_checker.dart';
import 'package:simf_app/features/splash/splash_controller.dart';

/// Page 001 — البداية · Splash / bootstrap. The KSA-Project Figma design
/// (node 159:573, D-361): the brand mark over "SAUDI · MOD · RSNF", the forum
/// name, and the edition/date lines, centred on the navy primary surface.
///
/// Shows the lock-up while [SplashController] runs the boot sequence
/// (version-policy update check (D-736) + cold-start session restore), then
/// routes out once. The previous placeholder screen is parked in
/// `_legacy_mockup/`.
///
/// Route: `RouteNames.splash`.
/// Data: [appUpdateCheckerProvider], [orgProfileProvider],
///       [splashControllerProvider].
/// Perf: no list — a single-screen layout.
class SplashScreen extends ConsumerStatefulWidget {
  const SplashScreen({super.key});

  @override
  ConsumerState<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<SplashScreen> {
  /// Guards the one-shot route-out / dialog so a rebuild never fires it twice.
  bool _handled = false;

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(splashControllerProvider);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        _handle(state);
      }
    });

    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navy,
      body: Center(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              const SimfLogo(size: SimfTokens.splashScreenSize),
              const SizedBox(height: SimfTokens.space2),
              Text(
                l10n.splashTagline,
                textAlign: TextAlign.center,
                style: SimfTokens.bodyBeigeLg,
              ),
              const SizedBox(height: SimfTokens.space10),
              Text(
                l10n.splashTitle,
                textAlign: TextAlign.center,
                style: SimfTokens.labelWhiteSemibold24Tall,
              ),
              const SizedBox(height: SimfTokens.space6),
              Text(
                _eventLine(l10n, ref.watch(orgProfileProvider)),
                textAlign: TextAlign.center,
                style: SimfTokens.bodyBeigeTitleTall,
              ),
            ],
          ),
        ),
      ),
    );
  }

  /// The edition + date/location lock-up line (#40-residual). The dates come
  /// from the CP-configured organization profile — hydrated synchronously from
  /// the on-device cache, so it is already present on the first splash frame of
  /// every run after the first. The bundled [AppL10n.splashEventLine] literal
  /// is the fallback only (first-ever run, or an edition with no dates set), so
  /// a new edition never ships a stale hardcoded date.
  String _eventLine(AppL10n l10n, OrgProfile? profile) {
    final dates = profile?.eventDateRange(isArabic: l10n.isArabic);
    if (dates == null || dates.isEmpty) {
      return l10n.splashEventLine;
    }
    final location = profile?.locationFor(isArabic: l10n.isArabic);
    if (location == null || location.isEmpty) {
      return '${l10n.splashEditionLine}\n$dates';
    }
    return '${l10n.splashEditionLine}\n$dates · $location';
  }

  void _handle(SplashState state) {
    if (_handled) {
      return;
    }
    switch (state) {
      case SplashLoading():
        return;
      case SplashUpdateRequired():
        // A forced update blocks boot: a non-dismissible dialog, no route-out.
        _handled = true;
        unawaited(_showUpdateDialog(hard: true));
      case SplashReady(:final routeName, :final location, :final softUpdate):
        _handled = true;
        if (softUpdate) {
          unawaited(_softUpdateThenRouteOut(routeName, location));
        } else {
          _routeOut(routeName: routeName, location: location);
        }
    }
  }

  /// Shows the dismissible soft-update prompt, then routes out **however** the
  /// dialog was closed (Later, "Update now" → store, or scrim) so the user is
  /// never stranded on the splash (Page_001 Logic L-6).
  Future<void> _softUpdateThenRouteOut(
    String? routeName,
    String? location,
  ) async {
    await _showUpdateDialog(hard: false);
    // ref is unusable once this State is disposed, and _routeOut would no-op
    // anyway, so bail rather than throw.
    if (!mounted) {
      return;
    }
    // D-736 — however the prompt was dismissed, snooze this version so it
    // doesn't re-nag on every launch (the About manual check still surfaces
    // it).
    unawaited(ref.read(appUpdateCheckerProvider).snoozeOptionalUpdate());
    _routeOut(routeName: routeName, location: location);
  }

  void _routeOut({String? routeName, String? location}) {
    if (!mounted) {
      return;
    }
    // Replace-navigate so the splash leaves the back stack (Page_001_Design).
    if (location != null) {
      context.go(location);
    } else if (routeName != null) {
      context.goNamed(routeName);
    }
  }

  Future<void> _showUpdateDialog({required bool hard}) async {
    final l10n = AppL10n.of(context);
    await showDialog<void>(
      context: context,
      // A forced update cannot be dismissed; a soft one can.
      barrierDismissible: !hard,
      builder: (dialogContext) {
        // A forced update also blocks the Android back button — the barrier
        // alone leaves it dismissible, which would strand the user on a bare
        // splash (D-736). A soft dialog stays freely dismissible.
        return PopScope(
          canPop: !hard,
          child: AlertDialog(
            title: Text(
              hard ? l10n.updateRequiredTitle : l10n.updateOptionalTitle,
            ),
            content: Text(
              hard ? l10n.updateRequiredBody : l10n.updateOptionalBody,
            ),
            actions: <Widget>[
              if (!hard)
                TextButton(
                  onPressed: () => Navigator.of(dialogContext).pop(),
                  child: Text(l10n.updateLaterLabel),
                ),
              FilledButton(
                onPressed: () {
                  unawaited(
                    ref.read(appUpdateCheckerProvider).openStoreListing(),
                  );
                  // A soft update must still route out after the store opens
                  // (the user is coming back to the app) — pop so the awaiting
                  // _softUpdateThenRouteOut resumes. A forced update never
                  // pops; it stays blocked until the user actually updates.
                  if (!hard) {
                    Navigator.of(dialogContext).pop();
                  }
                },
                child: Text(l10n.updateNowLabel),
              ),
            ],
          ),
        );
      },
    );
  }
}
