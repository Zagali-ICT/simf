@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/myarea/widgets/identity_capture_view.dart';
import 'package:simf_app/features/myarea/widgets/identity_fallback_view.dart';

import 'golden_fonts.dart';

/// Goldens for the identity-verification (التحقق من الهوية) screen, D-404 →
/// clean-code D-610 → full-bleed Figma D-611 → live-only + liveness-prompt
/// restored (owner 2026-07-06, D-662): the capture is live-camera-only with the
/// liveness-step prompt overlaid; no manual shutter, no gallery. The live camera
/// can't render in the test runtime (no camera plugin), so parity is locked on
/// the deterministic states: the loading surface (a spinner), the ready surface
/// with the liveness prompt, and the "camera required" retry.
///   flutter test --update-goldens test/golden/identity_verification_golden_test.dart

Widget _host(Widget child) => MaterialApp(
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
        body: SafeArea(child: Builder(builder: (context) => child)),
      ),
    );

LiveCaptureView _capture(
  BuildContext context, {
  required bool ready,
  Widget? preview,
}) {
  final l10n = AppL10n.of(context);
  return LiveCaptureView(
    ready: ready,
    preview: preview,
    humanCheckLabel: l10n.livenessHumanCheckTitle,
    promptText: l10n.livenessSmilePrompt,
    promptLeading: const Text('😊', style: TextStyle(fontSize: 30)),
    stepIndex: 0,
    stepCount: 3,
  );
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Identity full-bleed capture @375x812 — Figma 758:4180 (loading)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      _host(Builder(builder: (c) => _capture(c, ready: false))),
    );
    await tester.pump();

    await expectLater(
      find.byType(LiveCaptureView),
      matchesGoldenFile('goldens/identity_capture.png'),
    );
  });

  testWidgets('Identity capture prompt @375x812 — liveness step over preview',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      _host(
        Builder(
          builder: (c) => _capture(
            c,
            ready: true,
            preview: const ColoredBox(color: SimfTokens.navy),
          ),
        ),
      ),
    );
    await tester.pump();

    await expectLater(
      find.byType(LiveCaptureView),
      matchesGoldenFile('goldens/identity_capture_prompt.png'),
    );
  });

  testWidgets('Identity camera-required retry @375x812 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      _host(
        Builder(
          builder: (context) => IdentityFallbackView(
            l10n: AppL10n.of(context),
            onRetry: () {},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(IdentityFallbackView),
      matchesGoldenFile('goldens/identity_fallback.png'),
    );
  });
}
