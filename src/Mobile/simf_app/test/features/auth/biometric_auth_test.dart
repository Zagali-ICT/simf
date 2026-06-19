import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/auth/biometric_auth.dart';

/// A controllable [BiometricAuth] — `implements` ignores the real constructor
/// (which needs a Ref + local_auth), so the nudge and the toggle can be driven
/// without the device plugin. `enable`/`disable` flip [enabled] so the
/// auto-disposed providers reflect the new state after invalidation.
class _FakeBiometricAuth implements BiometricAuth {
  _FakeBiometricAuth({this.available = true, this.enabled = false});

  bool available;
  bool enabled;
  int enableCalls = 0;
  int disableCalls = 0;
  BiometricEnableResult enableResult = BiometricEnableResult.ok;

  @override
  Future<bool> isAvailable() async => available;
  @override
  Future<bool> isEnabled() async => enabled;
  @override
  Future<BiometricEnableResult> enable() async {
    enableCalls++;
    if (enableResult == BiometricEnableResult.ok) {
      enabled = true;
    }
    return enableResult;
  }

  @override
  Future<void> disable() async {
    disableCalls++;
    enabled = false;
  }
}

const String _nudgeBody =
    'Use your face or fingerprint to sign in next time — no password needed.';

Future<void> _pump(
  WidgetTester tester,
  Widget home, {
  required _FakeBiometricAuth biometric,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        biometricAuthProvider.overrideWithValue(biometric),
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
        home: home,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

/// A host with a button that fires the post-sign-in nudge.
Widget _nudgeHost() => Consumer(
      builder: (context, ref, _) => Scaffold(
        body: Center(
          child: ElevatedButton(
            onPressed: () => maybeOfferBiometricEnrolment(context, ref),
            child: const Text('GO'),
          ),
        ),
      ),
    );

void main() {
  group('maybeOfferBiometricEnrolment nudge (D-441 / D-445)', () {
    testWidgets('available + not enabled → shows the notification nudge; '
        'tapping Enable enrols', (tester) async {
      final biometric = _FakeBiometricAuth(available: true, enabled: false);
      await _pump(tester, _nudgeHost(), biometric: biometric);

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();
      // The notification-style nudge (a SnackBar with an Enable action).
      expect(find.text(_nudgeBody), findsOneWidget);

      await tester.tap(find.text('Enable'));
      await tester.pumpAndSettle();
      expect(biometric.enableCalls, 1);
      expect(find.text('Face ID sign-in enabled'), findsOneWidget);
      // Flush the SnackBar auto-dismiss timers.
      await tester.pump(const Duration(seconds: 9));
    });

    testWidgets('Enable still enrols after the originating screen is gone',
        (tester) async {
      // Regression for the deferred action capturing the screen's WidgetRef:
      // the SnackBar outlives the sign-in/OTP screen (route change), so the
      // Enable callback must enrol via the lifetime-safe ProviderContainer.
      final biometric = _FakeBiometricAuth(available: true, enabled: false);
      final navKey = GlobalKey<NavigatorState>();
      await tester.pumpWidget(
        ProviderScope(
          overrides: <Override>[
            biometricAuthProvider.overrideWithValue(biometric),
          ],
          child: MaterialApp(
            navigatorKey: navKey,
            locale: const Locale('en'),
            supportedLocales: AppL10n.supportedLocales,
            localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
              ...AppL10n.localizationsDelegates,
              GlobalMaterialLocalizations.delegate,
              GlobalWidgetsLocalizations.delegate,
              GlobalCupertinoLocalizations.delegate,
            ],
            home: _nudgeHost(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();
      expect(find.text(_nudgeBody), findsOneWidget);

      // Replace the screen that fired the nudge — disposes its Consumer/ref.
      unawaited(
        navKey.currentState!.pushReplacement(
          MaterialPageRoute<void>(
            builder: (_) => const Scaffold(body: Text('NEXT')),
          ),
        ),
      );
      await tester.pumpAndSettle();

      // The SnackBar persists on the root messenger; Enable must still enrol.
      await tester.tap(find.text('Enable'));
      await tester.pumpAndSettle();
      expect(biometric.enableCalls, 1);
      await tester.pump(const Duration(seconds: 9));
    });

    testWidgets('already enabled → no nudge', (tester) async {
      final biometric = _FakeBiometricAuth(available: true, enabled: true);
      await _pump(tester, _nudgeHost(), biometric: biometric);

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();
      expect(find.text(_nudgeBody), findsNothing);
    });

    testWidgets('biometrics unavailable → no nudge', (tester) async {
      final biometric = _FakeBiometricAuth(available: false, enabled: false);
      await _pump(tester, _nudgeHost(), biometric: biometric);

      await tester.tap(find.text('GO'));
      await tester.pumpAndSettle();
      expect(find.text(_nudgeBody), findsNothing);
    });
  });

  group('FaceIdToggleTile (D-445)', () {
    testWidgets('hidden when the device has no usable biometric',
        (tester) async {
      final biometric = _FakeBiometricAuth(available: false, enabled: false);
      await _pump(
        tester,
        const Scaffold(body: FaceIdToggleTile()),
        biometric: biometric,
      );
      expect(find.byType(SwitchListTile), findsNothing);
    });

    testWidgets('shows when available; toggling on enrols and flips on',
        (tester) async {
      final biometric = _FakeBiometricAuth(available: true, enabled: false);
      await _pump(
        tester,
        const Scaffold(body: FaceIdToggleTile()),
        biometric: biometric,
      );

      final tile = find.byType(SwitchListTile);
      expect(tile, findsOneWidget);
      expect(tester.widget<SwitchListTile>(tile).value, isFalse);

      await tester.tap(tile);
      await tester.pumpAndSettle();
      expect(biometric.enableCalls, 1);
      expect(tester.widget<SwitchListTile>(tile).value, isTrue);
      await tester.pump(const Duration(seconds: 5));
    });

    testWidgets('toggling off revokes the device key', (tester) async {
      final biometric = _FakeBiometricAuth(available: true, enabled: true);
      await _pump(
        tester,
        const Scaffold(body: FaceIdToggleTile()),
        biometric: biometric,
      );

      final tile = find.byType(SwitchListTile);
      expect(tester.widget<SwitchListTile>(tile).value, isTrue);

      await tester.tap(tile);
      await tester.pumpAndSettle();
      expect(biometric.disableCalls, 1);
      expect(tester.widget<SwitchListTile>(tile).value, isFalse);
      await tester.pump(const Duration(seconds: 5));
    });
  });
}
