@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/splash/data/splash_controller.dart';
import 'package:simf_app/features/splash/splash_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the Splash / bootstrap screen against Figma frame
/// **159:573** (البداية). Regenerate:
///   flutter test --update-goldens test/golden/splash_golden_test.dart
///
/// Frame parity expected: the brand lock-up centred on the navy primary
/// surface — the logo, the "SAUDI · MOD · RSNF" tagline, the forum name, and
/// the edition/date lines. RTL.
///
/// The controller is pinned to [SplashLoading] so the boot timers + one-shot
/// route-out never fire — the screen holds the lock-up. A single `pump()`
/// (never `pumpAndSettle`) keeps the fixed frame.

class _StubSplashController extends SplashController {
  _StubSplashController(this._fixed);

  final SplashState _fixed;

  @override
  SplashState build() => _fixed;
}

/// No configured edition, so the event line renders the bundled literal the
/// Figma frame was drawn against (#40-residual keeps that as the fallback).
class _NoOrgProfile extends OrgProfileController {
  @override
  OrgProfile? build() => null;
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Splash @375x812 — Figma 159:573 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          splashControllerProvider
              .overrideWith(() => _StubSplashController(const SplashLoading())),
          orgProfileProvider.overrideWith(_NoOrgProfile.new),
        ],
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          locale: const Locale('ar'),
          supportedLocales: AppL10n.supportedLocales,
          localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
            ...AppL10n.localizationsDelegates,
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],
          home: const SplashScreen(),
        ),
      ),
    );
    await tester.pump();
    // The brand mark is an `Image.asset` PNG — precache it (real I/O) so it
    // rasterises in the golden, then paint the resolved frame. Only `pump()`
    // (never `pumpAndSettle`) is used — the stubbed SplashLoading has no boot
    // timers to settle.
    final context = tester.element(find.byType(SplashScreen));
    await tester.runAsync(() async {
      await precacheImage(
        const AssetImage('assets/images/simf_logo.png'),
        context,
      );
    });
    await tester.pump();

    await expectLater(
      find.byType(SplashScreen),
      matchesGoldenFile('goldens/splash_159-573.png'),
    );
  });
}
