import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../core/startup/app_update_checker.dart';
import 'splash_controller.dart';

/// Page 001 — البداية · Splash / bootstrap (Page_001_Design).
///
/// Shows the SIMF logo while [SplashController] runs the boot sequence
/// (store-update check + cold-start session restore), then routes out once.
/// The visuals are an interim placeholder — the final design lands with
/// SIMF-VID-001 — but the state + API wiring behind it is real.
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
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space8),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              const _LogoMark(),
              const SizedBox(height: SimfTokens.space6),
              Text(
                l10n.splashTagline,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: SimfTokens.txtTertiary,
                  fontSize: 9,
                  letterSpacing: 2,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: SimfTokens.space3),
              Text(
                l10n.splashTitle,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: SimfTokens.surface,
                  fontSize: 17,
                  fontWeight: FontWeight.w700,
                  height: 1.5,
                ),
              ),
              const SizedBox(height: SimfTokens.space2),
              Text(
                l10n.splashEventLine,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: SimfTokens.txtSecondary,
                  fontSize: 11,
                  height: 1.7,
                ),
              ),
            ],
          ),
        ),
      ),
    );
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
        return AlertDialog(
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
              },
              child: Text(l10n.updateNowLabel),
            ),
          ],
        );
      },
    );
  }
}

/// The interim brass-on-navy logo placeholder. Replaced by the real asset with
/// SIMF-VID-001; no image asset is bundled yet (Page_001_Design — Design notes).
/// Matches the mockup splash lockup: a hairline gold ring with the "SIMF"
/// wordmark in gold, not a filled disc.
class _LogoMark extends StatelessWidget {
  const _LogoMark();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 68,
      height: 68,
      decoration: const BoxDecoration(
        shape: BoxShape.circle,
        border: Border.fromBorderSide(
          BorderSide(color: SimfTokens.accent),
        ),
      ),
      alignment: Alignment.center,
      child: const Text(
        'SIMF',
        style: TextStyle(
          color: SimfTokens.accent,
          fontWeight: FontWeight.w700,
          fontSize: SimfTokens.textSm,
          letterSpacing: 2,
        ),
      ),
    );
  }
}
