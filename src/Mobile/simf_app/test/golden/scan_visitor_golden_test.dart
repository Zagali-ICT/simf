@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/exhibitor/scan_visitor_screen.dart';

import 'golden_fonts.dart';

/// Render-lock golden of the exhibitor "scan a visitor badge" screen
/// (مسح بطاقة زائر, D-426). Regenerate:
///   flutter test --update-goldens test/golden/scan_visitor_golden_test.dart
///
/// No Figma frame is bound (a D-426 functional page); the screen delegates
/// entirely to the shared `QrScanView`, so this locks that shared scan surface —
/// the manual-entry field + the "start camera" button (`enableCamera: false`, so
/// no live camera in the harness). RTL.

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Scan visitor @375x812 — QrScanView surface (Arabic)',
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
          home: const ScanVisitorScreen(enableCamera: false),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(ScanVisitorScreen),
      matchesGoldenFile('goldens/scan_visitor.png'),
    );
  });
}
