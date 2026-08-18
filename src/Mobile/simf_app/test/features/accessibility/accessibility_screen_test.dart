import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/accessibility/accessibility_screen.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';
import '_fake_prefs.dart';

Future<void> _pump(WidgetTester tester, FakePrefs prefs) async {
  await tester.pumpWidget(
    simfTestScope(
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

/// The Switch inside the row that carries [title] (frame 1116:16630).
Finder _switchFor(String title) => find.descendant(
      of: find.ancestor(of: find.text(title), matching: find.byType(Row)).first,
      matching: find.byType(Switch),
    );

void main() {
  group('AccessibilityScreen (Page 038 — frame 1116:16630)', () {
    testWidgets('renders the display + sound sections and their controls',
        (tester) async {
      await _pump(tester, FakePrefs());
      // العرض — four font-size chips.
      expect(find.text('Display'), findsOneWidget);
      expect(find.text('Small'), findsOneWidget);
      expect(find.text('Medium'), findsOneWidget);
      expect(find.text('Large'), findsOneWidget);
      expect(find.text('Extra large'), findsOneWidget);
      // Four switches: high contrast, reduce motion, screen reader, captions.
      expect(find.byType(Switch), findsNWidgets(4));
      expect(find.text('High contrast'), findsOneWidget);
      expect(find.text('Reduce motion'), findsOneWidget);
      // الصوت والقراءة section.
      expect(find.text('Sound & reading'), findsOneWidget);
      expect(find.text('Screen reader'), findsOneWidget);
      expect(find.text('Captions (for sessions)'), findsOneWidget);
    });

    testWidgets('toggling high-contrast flips it and persists', (tester) async {
      final prefs = FakePrefs();
      await _pump(tester, prefs);
      final highContrast = _switchFor('High contrast');
      expect(tester.widget<Switch>(highContrast).value, isFalse);
      await tester.tap(highContrast);
      await tester.pumpAndSettle();
      expect(tester.widget<Switch>(highContrast).value, isTrue);
      expect(prefs.getBool(StorageKeys.accessibilityHighContrast), isTrue);
    });

    testWidgets('picking a text size persists the choice', (tester) async {
      final prefs = FakePrefs();
      await _pump(tester, prefs);
      await tester.tap(find.text('Extra large'));
      await tester.pumpAndSettle();
      expect(
        prefs.getString(StorageKeys.accessibilityTextSize),
        AppTextSize.extraLarge.name,
      );
    });

    testWidgets('screen-reader assist defaults off and persists on',
        (tester) async {
      final prefs = FakePrefs();
      await _pump(tester, prefs);
      final screenReader = _switchFor('Screen reader');
      expect(tester.widget<Switch>(screenReader).value, isFalse);
      await tester.tap(screenReader);
      await tester.pumpAndSettle();
      expect(prefs.getBool(StorageKeys.accessibilityScreenReader), isTrue);
    });

    testWidgets('captions default on and persist off', (tester) async {
      final prefs = FakePrefs();
      await _pump(tester, prefs);
      final captions = _switchFor('Captions (for sessions)');
      expect(tester.widget<Switch>(captions).value, isTrue);
      await tester.tap(captions);
      await tester.pumpAndSettle();
      expect(prefs.getBool(StorageKeys.accessibilityCaptions), isFalse);
    });
  });
}
