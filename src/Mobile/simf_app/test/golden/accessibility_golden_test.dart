@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/accessibility/accessibility_screen.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';

import '../features/accessibility/_fake_prefs.dart';
import 'golden_fonts.dart';

/// Golden render of the Accessibility screen against Figma frame **1116:16630**
/// (إمكانية الوصول). Regenerate:
///   flutter test --update-goldens test/golden/accessibility_golden_test.dart
///
/// Frame parity expected: the navy shell, the العرض section (the حجم الخط card
/// with four pill chips — the default متوسط filled gold — then the high-contrast
/// and reduce-motion toggle rows), and the الصوت والقراءة section (the
/// screen-reader + captions toggle rows, captions on by default). RTL.

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Accessibility @375x812 — Figma 1116:16630 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          accessibilityControllerProvider
              .overrideWith(() => AccessibilityController(prefs: FakePrefs())),
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
          home: const AccessibilityScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(AccessibilityScreen),
      matchesGoldenFile('goldens/accessibility_1116-16630.png'),
    );
  });
}
