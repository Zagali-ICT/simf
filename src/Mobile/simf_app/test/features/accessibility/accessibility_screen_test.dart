import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/accessibility/accessibility_screen.dart';

Future<void> _pump(WidgetTester tester) async {
  await tester.pumpWidget(
    const MaterialApp(
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
  );
  await tester.pumpAndSettle();
}

void main() {
  group('AccessibilityScreen (Page 038)', () {
    testWidgets('renders the three controls', (tester) async {
      await _pump(tester);
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

    testWidgets('toggling the high-contrast switch flips it', (tester) async {
      await _pump(tester);
      final highContrast = find.widgetWithText(SwitchListTile, 'High contrast');
      expect(tester.widget<SwitchListTile>(highContrast).value, isFalse);
      await tester.tap(highContrast);
      await tester.pumpAndSettle();
      expect(tester.widget<SwitchListTile>(highContrast).value, isTrue);
    });
  });
}
