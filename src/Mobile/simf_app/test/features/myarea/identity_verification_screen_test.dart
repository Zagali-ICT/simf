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

    test('invertYaw normalises the iOS sign (physical right = negative raw)',
        () {
      // On iOS the front-camera mirror + per-platform input-image rotation
      // report the OPPOSITE sign, so a physical RIGHT turn arrives as a
      // negative raw yaw. invertYaw normalises it so positive means right.
      expect(
        livenessStepSatisfied(
          LivenessStep.turnRight,
          headEulerAngleY: -30,
          invertYaw: true,
        ),
        isTrue,
      );
      expect(
        livenessStepSatisfied(
          LivenessStep.turnRight,
          headEulerAngleY: 30,
          invertYaw: true,
        ),
        isFalse,
      );
      expect(
        livenessStepSatisfied(
          LivenessStep.turnLeft,
          headEulerAngleY: 30,
          invertYaw: true,
        ),
        isTrue,
      );
      expect(
        livenessStepSatisfied(
          LivenessStep.turnLeft,
          headEulerAngleY: -30,
          invertYaw: true,
        ),
        isFalse,
      );
    });

    test('turn gate is inclusive at exactly the ±threshold boundary', () {
      // Guards `>=`/`<=` (vs a `>`/`<` mutation) and the kTurnYawDegrees
      // constant.
      expect(
        livenessStepSatisfied(
          LivenessStep.turnRight,
          headEulerAngleY: kTurnYawDegrees,
        ),
        isTrue,
      );
      expect(
        livenessStepSatisfied(
          LivenessStep.turnRight,
          headEulerAngleY: kTurnYawDegrees - 1,
        ),
        isFalse,
      );
      expect(
        livenessStepSatisfied(
          LivenessStep.turnLeft,
          headEulerAngleY: -kTurnYawDegrees,
        ),
        isTrue,
      );
      expect(
        livenessStepSatisfied(
          LivenessStep.turnLeft,
          headEulerAngleY: -(kTurnYawDegrees - 1),
        ),
        isFalse,
      );
      // Boundary also holds after inversion: raw -20 → normalised +20.
      expect(
        livenessStepSatisfied(
          LivenessStep.turnRight,
          headEulerAngleY: -kTurnYawDegrees,
          invertYaw: true,
        ),
        isTrue,
      );
    });
  });

  group('liveness platform + direction helpers', () {
    test('livenessInvertYaw: iOS never inverts; Android inverts by sensor',
        () {
      // iOS feeds the raw sensor rotation + a mirrored preview, so a physical
      // RIGHT turn already reads positive — never invert (any sensor value).
      expect(livenessInvertYaw(TargetPlatform.iOS, 270), isFalse);
      expect(livenessInvertYaw(TargetPlatform.iOS, 90), isFalse);
      // Android is sensor-orientation dependent: 270 (the common front camera +
      // the RSNF tablet) reads a physical RIGHT turn negative → invert; 90 is
      // the mirror case → do not invert. Threshold is `>= 180`.
      expect(livenessInvertYaw(TargetPlatform.android, 270), isTrue);
      expect(livenessInvertYaw(TargetPlatform.android, 180), isTrue);
      expect(livenessInvertYaw(TargetPlatform.android, 90), isFalse);
      expect(livenessInvertYaw(TargetPlatform.android, 0), isFalse);
    });

    test('livenessPromptDirection matches the step name (no platform branch)',
        () {
      expect(
        livenessPromptDirection(LivenessStep.smile),
        LivenessPromptDirection.none,
      );
      expect(
        livenessPromptDirection(LivenessStep.turnRight),
        LivenessPromptDirection.right,
      );
      expect(
        livenessPromptDirection(LivenessStep.turnLeft),
        LivenessPromptDirection.left,
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
