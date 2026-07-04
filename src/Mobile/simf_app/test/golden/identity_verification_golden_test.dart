@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/myarea/data/liveness.dart';
import 'package:simf_app/features/myarea/widgets/identity_capture_view.dart';
import 'package:simf_app/features/myarea/widgets/identity_fallback_view.dart';

import 'golden_fonts.dart';

/// Goldens for the identity-verification (التحقق من الهوية) screen, D-404 →
/// clean-code freeze D-610. The live-camera path can't render in the test
/// runtime (no camera plugin) and the challenge order is shuffled per session
/// (D-422, non-deterministic), so — like live_broadcast (D-603) — the parity is
/// locked on the deterministic sub-widgets: the capture **chrome** (gold-framed
/// preview box + prompt + progress bar) and the gallery **fallback**.
///   flutter test --update-goldens test/golden/identity_verification_golden_test.dart
///
/// The capture golden deliberately renders `step: turnLeft` (enum index 2) with
/// `activeIndex: 0` — the D-610 progress-dots fix means exactly ONE gold bar
/// shows (the first of three); the pre-fix code drove the bar off `step.index`
/// and would have lit all three on the very first step.

Widget _host(Widget child, {required Size size}) => MaterialApp(
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
      home: Scaffold(
        backgroundColor: SimfTokens.navy,
        body: SafeArea(
          child: Builder(
            builder: (context) => child,
          ),
        ),
      ),
    );

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Identity capture chrome @375x812 — Figma 758:4316 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      _host(
        Builder(
          builder: (context) => LiveCaptureView(
            l10n: AppL10n.of(context),
            step: LivenessStep.turnLeft,
            activeIndex: 0,
            ready: false,
            preview: null,
          ),
        ),
        size: const Size(375, 812),
      ),
    );
    await tester.pump();

    await expectLater(
      find.byType(LiveCaptureView),
      matchesGoldenFile('goldens/identity_capture_758-4316.png'),
    );
  });

  testWidgets('Identity gallery fallback @375x812 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      _host(
        Builder(
          builder: (context) => IdentityFallbackView(
            l10n: AppL10n.of(context),
            onPick: () {},
          ),
        ),
        size: const Size(375, 812),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(IdentityFallbackView),
      matchesGoldenFile('goldens/identity_fallback.png'),
    );
  });
}
