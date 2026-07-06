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
/// clean-code D-610 → full-bleed Figma redesign D-611 → manual shutter + gallery
/// re-added (owner 2026-07-06) so the photo can always be taken. The live camera
/// can't render in the test runtime (no camera plugin), so parity is locked on
/// the deterministic states: the loading capture surface (a spinner until the
/// camera is ready), the ready surface with the shutter + prompt + gallery
/// overlay, and the gallery fallback.
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
    promptText: l10n.identityCapturePrompt,
    captureLabel: l10n.capturePhotoLabel,
    galleryLabel: l10n.chooseFromGallery,
    capturing: false,
    onCapture: () {},
    onGallery: () {},
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

  testWidgets('Identity capture controls @375x812 — shutter + prompt + gallery',
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
      matchesGoldenFile('goldens/identity_capture_controls.png'),
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
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(IdentityFallbackView),
      matchesGoldenFile('goldens/identity_fallback.png'),
    );
  });
}
