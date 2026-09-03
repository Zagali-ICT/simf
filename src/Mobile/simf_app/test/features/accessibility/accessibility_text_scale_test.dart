import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/accessibility/accessibility_screen.dart';
import 'package:simf_app/features/accessibility/data/accessibility_controller.dart';
import 'package:simf_app/features/accessibility/widgets/size_chip.dart';

import '../../support/simf_test_scope.dart';
import '_fake_prefs.dart';

Future<void> _pumpScreen(
  WidgetTester tester,
  FakePrefs prefs, {
  required double platformScale,
}) async {
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        accessibilityControllerProvider
            .overrideWith(() => AccessibilityController(prefs: prefs)),
      ],
      child: MaterialApp(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(context)
              .copyWith(textScaler: TextScaler.linear(platformScale)),
          child: child ?? const SizedBox.shrink(),
        ),
        home: const AccessibilityScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('composedTextScaler', () {
    test('composes the app choice ON TOP of the platform, not instead of it',
        () {
      // The bug this replaced: TextScaler.linear(choice) threw the platform
      // scaler away, so a user who had already enlarged text system-wide saw
      // an app that did not respond to it at all.
      const platform = TextScaler.linear(1.2);
      expect(
        composedTextScaler(platform, AppTextSize.normal).scale(10),
        closeTo(12, 0.001),
      );
      expect(
        composedTextScaler(TextScaler.noScaling, AppTextSize.normal).scale(10),
        closeTo(10, 0.001),
      );
    });

    test('the four choices stay distinct at ordinary platform scales', () {
      for (final platform in <double>[0.9, 1, 1.1]) {
        final results = AppTextSize.values
            .map(
              (s) => composedTextScaler(
                TextScaler.linear(platform),
                s,
              ).scale(10),
            )
            .toList();
        expect(
          results.toSet(),
          hasLength(AppTextSize.values.length),
          reason: 'chips collapsed at platform scale $platform: $results',
        );
      }
    });

    test('the composite never exceeds the ceiling the screens are drawn for',
        () {
      // iOS accessibility Dynamic Type reaches roughly 3.1. Multiplying that
      // by the app's own 1.3 would hand the layout a scale nothing here was
      // built for, and the day-card overflow proved fixed heights are real.
      for (final platform in <double>[1.5, 2, 3.1]) {
        for (final size in AppTextSize.values) {
          expect(
            composedTextScaler(TextScaler.linear(platform), size).scale(10),
            lessThanOrEqualTo(maxTextScale * 10 + 0.001),
          );
        }
      }
    });

    test('a tiny platform scale never shrinks past the floor', () {
      expect(
        composedTextScaler(const TextScaler.linear(0.5), AppTextSize.small)
            .scale(10),
        closeTo(minTextScale * 10, 0.001),
      );
    });

    test('platformTextScaleAtCeiling marks where the chips stop mattering', () {
      expect(platformTextScaleAtCeiling(TextScaler.noScaling), isFalse);
      expect(
        platformTextScaleAtCeiling(const TextScaler.linear(1.29)),
        isFalse,
      );
      expect(platformTextScaleAtCeiling(const TextScaler.linear(1.3)), isTrue);
      expect(platformTextScaleAtCeiling(const TextScaler.linear(3.1)), isTrue);
    });
  });

  group('AccessibilityScreen font-size card honesty', () {
    testWidgets('the chips are live at an ordinary platform scale',
        (tester) async {
      await _pumpScreen(tester, FakePrefs(), platformScale: 1);
      expect(
        find.textContaining('system setting is in control'),
        findsNothing,
      );
      expect(
        tester.widgetList<SizeChip>(find.byType(SizeChip)).every(
              (chip) => chip.onTap != null,
            ),
        isTrue,
      );
    });

    testWidgets('at the ceiling the chips are DISABLED and say why',
        (tester) async {
      // Otherwise all four render identically while still filling gold and
      // still persisting - a live-looking control that changes nothing, which
      // is the complaint this whole screen was cut down to remove.
      await _pumpScreen(tester, FakePrefs(), platformScale: 3.1);
      expect(
        find.textContaining('system setting is in control'),
        findsOneWidget,
      );
      expect(
        tester.widgetList<SizeChip>(find.byType(SizeChip)).every(
              (chip) => chip.onTap == null,
            ),
        isTrue,
      );
    });
  });
}
