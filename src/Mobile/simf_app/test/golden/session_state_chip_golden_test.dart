@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/widgets/session_state_chip.dart';

import 'golden_fonts.dart';

/// Golden of the session-state chips (owner 2026-07-14) so the new UI has
/// visual regression + a design reference. Deterministic — the chips are
/// rendered by [SessionChipKind] directly (not phase-derived), so this golden
/// is stable over time. Regenerate: flutter test --update-goldens
/// test/golden/session_state_chip_golden_test.dart
///
/// The chips: مباشر الآن (live, red), الملخص متاح (summary ready, gold
/// outline), مسجّل (recorded, gold fill) — each a rounded pill with a trailing
/// dot, on the navy card fill they sit on in the agenda / my-sessions / summary
/// cards.

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Session-state chips (Arabic, on navy card)', (tester) async {
    tester.view.physicalSize = const Size(375, 220);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      MaterialApp(
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
          body: Center(
            child: Container(
              margin: const EdgeInsets.all(SimfTokens.space4),
              padding: const EdgeInsets.all(SimfTokens.space4),
              decoration: BoxDecoration(
                color: SimfTokens.navyDeep,
                borderRadius: BorderRadius.circular(SimfTokens.radius),
              ),
              child: const Wrap(
                spacing: SimfTokens.space2,
                runSpacing: SimfTokens.space2,
                children: <Widget>[
                  SessionStateChip(kind: SessionChipKind.live, label: 'مباشر الآن'),
                  SessionStateChip(
                    kind: SessionChipKind.summaryReady,
                    label: 'الملخص متاح',
                  ),
                  SessionStateChip(
                    kind: SessionChipKind.recorded,
                    label: 'مسجّل',
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(Scaffold),
      matchesGoldenFile('goldens/session_state_chips.png'),
    );
  });
}
