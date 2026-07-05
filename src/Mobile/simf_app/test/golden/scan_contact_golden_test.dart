@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/contacts/scan_contact_screen.dart';

import 'golden_fonts.dart';

/// Render-lock golden of the "Scan a contact" screen against the owner-supplied
/// frame **1701:7080** (مسح جهة اتصال, FDS-014). Regenerate:
///   flutter test --update-goldens test/golden/scan_contact_golden_test.dart
///
/// The screen delegates its surface to the shared `QrScanView`; the resolved-card
/// preview sheet only appears after a scan, so this locks the resting scan
/// surface — the manual-entry field + the "start camera" button
/// (`enableCamera: false`, so no live camera in the harness). RTL.

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Scan contact @375x812 — Figma 1701:7080 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      ProviderScope(
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
          home: const ScanContactScreen(enableCamera: false),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(ScanContactScreen),
      matchesGoldenFile('goldens/scan_contact_1701-7080.png'),
    );
  });
}
