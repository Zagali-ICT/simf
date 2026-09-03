import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
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

void main() {
  group('AccessibilityScreen (Page 038 — frame 1116:16630)', () {
    testWidgets('renders the display section and the four size chips',
        (tester) async {
      await _pump(tester, FakePrefs());
      expect(find.text('Display'), findsOneWidget);
      expect(find.text('Small'), findsOneWidget);
      expect(find.text('Medium'), findsOneWidget);
      expect(find.text('Large'), findsOneWidget);
      expect(find.text('Extra large'), findsOneWidget);
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

    testWidgets('offers NO control that does not work', (tester) async {
      // The screen used to carry four switches - high contrast, reduce motion,
      // the screen-reader announcer and captions - each wired to a provider and
      // each observably inert. Apple rejects non-functional features, and these
      // were reachable SIGNED OUT, so a reviewer needed no account to
      // find them.
      await _pump(tester, FakePrefs());
      expect(find.byType(Switch), findsNothing);
      for (final gone in <String>[
        'High contrast',
        'Reduce motion',
        'Sound & reading',
        'Screen reader',
        'Captions (for sessions)',
      ]) {
        expect(
          find.text(gone),
          findsNothing,
          reason: '"$gone" did not work',
        );
      }
    });

    testWidgets('keeps the persisted shape so nothing stored is orphaned',
        (tester) async {
      // The controller fields and storage keys survive the withdrawal: a value
      // already on a device (or synced from the server) still round-trips, so
      // restoring a control later is a UI change and not a migration.
      final prefs = FakePrefs(<String, Object>{
        StorageKeys.accessibilityHighContrast: true,
        StorageKeys.accessibilityCaptions: false,
      });
      await _pump(tester, prefs);
      final settings = ProviderScope.containerOf(
        tester.element(find.byType(AccessibilityScreen)),
      ).read(accessibilityControllerProvider);
      expect(settings.highContrast, isTrue);
      expect(settings.captions, isFalse);
    });
  });
}
