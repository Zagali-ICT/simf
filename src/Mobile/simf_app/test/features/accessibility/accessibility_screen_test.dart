import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/accessibility/accessibility_screen.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '_fake_prefs.dart';

Future<void> _pump(WidgetTester tester, FakePrefs prefs) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        accessibilityControllerProvider
            .overrideWith(() => AccessibilityController(prefs: prefs)),
      ],
      child: const MaterialApp(
        locale: Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: AccessibilityScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('AccessibilityScreen (Page 038)', () {
    testWidgets('renders the three controls', (tester) async {
      await _pump(tester, FakePrefs());
      // Text-size choice (three chips).
      expect(find.byType(ChoiceChip), findsNWidgets(3));
      expect(find.text('Small'), findsOneWidget);
      expect(find.text('Default'), findsOneWidget);
      expect(find.text('Large'), findsOneWidget);
      // High-contrast + reduce-motion switches.
      expect(find.byType(SwitchListTile), findsNWidgets(2));
      expect(find.text('High contrast'), findsOneWidget);
      expect(find.text('Reduce motion'), findsOneWidget);
    });

    testWidgets('toggling high-contrast flips it and persists', (tester) async {
      final prefs = FakePrefs();
      await _pump(tester, prefs);
      final highContrast = find.widgetWithText(SwitchListTile, 'High contrast');
      expect(tester.widget<SwitchListTile>(highContrast).value, isFalse);
      await tester.tap(highContrast);
      await tester.pumpAndSettle();
      expect(tester.widget<SwitchListTile>(highContrast).value, isTrue);
      expect(prefs.getBool(StorageKeys.accessibilityHighContrast), isTrue);
    });

    testWidgets('picking a text size persists the choice', (tester) async {
      final prefs = FakePrefs();
      await _pump(tester, prefs);
      await tester.tap(find.widgetWithText(ChoiceChip, 'Large'));
      await tester.pumpAndSettle();
      expect(
        prefs.getString(StorageKeys.accessibilityTextSize),
        AppTextSize.large.name,
      );
    });
  });
}
