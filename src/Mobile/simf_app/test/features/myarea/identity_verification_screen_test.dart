import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/myarea/identity_verification_screen.dart';

Widget _wrap(Widget home, {Locale locale = const Locale('en')}) => MaterialApp(
      locale: locale,
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: home,
    );

void main() {
  group('livenessStepSatisfied (D-404 pure step gate)', () {
    test('smile needs smilingProbability ≥ threshold', () {
      expect(
        livenessStepSatisfied(LivenessStep.smile, smilingProbability: 0.9),
        isTrue,
      );
      expect(
        livenessStepSatisfied(LivenessStep.smile, smilingProbability: 0.3),
        isFalse,
      );
      // No classification value → not satisfied (never auto-passes).
      expect(livenessStepSatisfied(LivenessStep.smile), isFalse);
    });

    test('turn-right needs a positive yaw beyond the threshold', () {
      expect(
        livenessStepSatisfied(LivenessStep.turnRight, headEulerAngleY: 30),
        isTrue,
      );
      expect(
        livenessStepSatisfied(LivenessStep.turnRight, headEulerAngleY: 5),
        isFalse,
      );
      expect(
        livenessStepSatisfied(LivenessStep.turnRight, headEulerAngleY: -30),
        isFalse,
      );
    });

    test('turn-left needs a negative yaw beyond the threshold', () {
      expect(
        livenessStepSatisfied(LivenessStep.turnLeft, headEulerAngleY: -30),
        isTrue,
      );
      expect(
        livenessStepSatisfied(LivenessStep.turnLeft, headEulerAngleY: -5),
        isFalse,
      );
      expect(
        livenessStepSatisfied(LivenessStep.turnLeft, headEulerAngleY: 30),
        isFalse,
      );
    });
  });

  testWidgets('the screen builds with the header and a loading preview '
      '(camera init is async; no overflow)', (tester) async {
    await tester.pumpWidget(_wrap(const IdentityVerificationScreen()));
    // One frame only — the native camera init never resolves in the test
    // runtime, so the screen stays on the loading preview (not settled).
    await tester.pump();

    expect(find.text('Identity verification'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    // No layout overflow on either the loading or fallback path.
    expect(tester.takeException(), isNull);
  });
}
