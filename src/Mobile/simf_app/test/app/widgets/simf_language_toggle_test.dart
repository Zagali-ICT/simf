import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_language_toggle.dart';

Future<void> _pump(WidgetTester tester, {required Locale locale}) async {
  await tester.pumpWidget(
    MaterialApp(
      locale: locale,
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      // A tight leading row like the header, so a too-wide label overflows
      // (the pill's FittedBox must keep the "ع" label within 48px).
      home: Scaffold(
        body: Row(
          children: <Widget>[
            SimfLanguageToggle(onPressed: () {}),
          ],
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('SimfLanguageToggle (Figma 1967:3661)', () {
    testWidgets('shows "EN" (switch to English) when Arabic is active',
        (tester) async {
      await _pump(tester, locale: const Locale('ar'));
      expect(find.text('EN'), findsOneWidget);
      expect(find.text('ع'), findsNothing);
    });

    testWidgets('shows "ع" (switch to Arabic) when English is active (D-697)',
        (tester) async {
      await _pump(tester, locale: const Locale('en'));
      expect(find.text('ع'), findsOneWidget);
      expect(find.text('ع ر'), findsNothing); // the old spaced label is gone
      expect(find.text('EN'), findsNothing);
    });

    testWidgets('the "ع" label fits the 48px pill (no overflow)',
        (tester) async {
      await _pump(tester, locale: const Locale('en'));
      // A RenderFlex overflow throws during layout and is recorded here; the
      // FittedBox in the pill must scale the label so this stays empty.
      expect(tester.takeException(), isNull);
    });
  });
}
