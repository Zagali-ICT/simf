import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/biometric_auth.dart';
import 'package:simf_app/features/account/biometric_step_up_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-738 — a controllable [BiometricAuth] so the OS device-credential confirm
/// step at enrolment can be scripted without the device plugin.
class _FakeBiometric implements BiometricAuth {
  _FakeBiometric({this.outcome = LocalAuthOutcome.success});

  LocalAuthOutcome outcome;
  int confirmCalls = 0;

  @override
  Future<LocalAuthOutcome> confirmDeviceIdentity(String reason) async {
    confirmCalls++;
    return outcome;
  }

  @override
  Future<bool> isAvailable() async => true;
  @override
  Future<bool> isEnabled() async => false;
  @override
  Future<void> disable() async {}
}

/// #7a — a fake AuthController for the biometric step-up screen: `build()`
/// returns SignedOut (the screen never reads state), `sendBiometricStepUp`
/// returns a masked email, and `enrolDeviceKey` records the supplied code and
/// optionally throws (a wrong/expired code).
class _FakeController extends AuthController {
  _FakeController({this.enrolFailure, this.maskedEmail = 't***@simf.sa'});

  final AuthFailure? enrolFailure;
  final String maskedEmail;
  bool sendCalled = false;
  String? enrolledWithCode;

  @override
  AuthState build() => const AuthStateSignedOut();

  @override
  Future<String> sendBiometricStepUp() async {
    sendCalled = true;
    return maskedEmail;
  }

  @override
  Future<void> enrolDeviceKey({
    String label = 'SIMF mobile',
    String? stepUpCode,
  }) async {
    enrolledWithCode = stepUpCode;
    if (enrolFailure != null) {
      // AuthFailure is a sealed RESULT type: production returns it, never
      // throws. A fake repository throws it to drive the failure path a screen
      // handles, so it is deliberately neither an Exception nor an Error.
      // ignore: only_throw_errors
      throw enrolFailure!;
    }
  }
}

Future<void> _pump(
  WidgetTester tester,
  _FakeController controller, {
  _FakeBiometric? biometric,
}) async {
  final router = GoRouter(
    initialLocation: '/home',
    routes: <RouteBase>[
      GoRoute(
        path: '/home',
        builder: (c, s) => const Scaffold(body: Text('HOME')),
      ),
      GoRoute(
        name: RouteNames.biometricStepUp,
        path: '/auth/biometric-step-up',
        builder: (c, s) => const BiometricStepUpScreen(),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        authControllerProvider.overrideWith(() => controller),
        biometricAuthProvider.overrideWithValue(
          biometric ?? _FakeBiometric(),
        ),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
      ),
    ),
  );
  await tester.pumpAndSettle();
  unawaited(router.pushNamed(RouteNames.biometricStepUp));
  await tester.pumpAndSettle();
}

void main() {
  group('BiometricStepUpScreen (#7a)', () {
    testWidgets('on open it requests a code and shows the masked recipient',
        (tester) async {
      final controller = _FakeController(maskedEmail: 'd***@simf.test');
      await _pump(tester, controller);

      expect(controller.sendCalled, isTrue);
      expect(find.text('d***@simf.test'), findsOneWidget);

      await tester.pumpWidget(const SizedBox()); // cancel the resend timer
    });

    testWidgets('entering a code enrols with it and pops with a success toast',
        (tester) async {
      final controller = _FakeController();
      await _pump(tester, controller);

      await tester.enterText(find.byType(TextField), '123456');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Verify'));
      await tester.pumpAndSettle();

      expect(controller.enrolledWithCode, '123456');
      // Popped back to HOME + the success toast on the root messenger.
      expect(find.text('HOME'), findsOneWidget);
      expect(find.text('Face ID sign-in enabled'), findsOneWidget);

      await tester.pump(const Duration(seconds: 5)); // flush the SnackBar timer
    });

    testWidgets('cancelling the device-credential confirm does NOT enrol and '
        'stays on the screen (D-738)', (tester) async {
      final controller = _FakeController();
      final biometric = _FakeBiometric(outcome: LocalAuthOutcome.cancelled);
      await _pump(tester, controller, biometric: biometric);

      await tester.enterText(find.byType(TextField), '123456');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Verify'));
      await tester.pumpAndSettle();

      expect(biometric.confirmCalls, 1);
      expect(controller.enrolledWithCode, isNull); // never enrolled
      expect(find.text('HOME'), findsNothing); // stayed on the screen
      expect(
        find.text('Confirmation cancelled — Face ID sign-in was not enabled.'),
        findsOneWidget,
      );

      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('no device screen lock shows the set-a-lock message (D-738)',
        (tester) async {
      final controller = _FakeController();
      final biometric =
          _FakeBiometric(outcome: LocalAuthOutcome.noDeviceCredential);
      await _pump(tester, controller, biometric: biometric);

      await tester.enterText(find.byType(TextField), '123456');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Verify'));
      await tester.pumpAndSettle();

      expect(controller.enrolledWithCode, isNull);
      expect(
        find.textContaining('Set a device screen lock'),
        findsOneWidget,
      );

      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('a wrong code shows the inline error and stays on the screen',
        (tester) async {
      final controller = _FakeController(
        enrolFailure: const UnknownAuthFailure(
          ApiFailure(
            code: 'BIOMETRIC_STEP_UP_INVALID',
            message: 'The verification code is incorrect.',
            httpStatus: 401,
          ),
        ),
      );
      await _pump(tester, controller);

      await tester.enterText(find.byType(TextField), '999999');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Verify'));
      await tester.pumpAndSettle();

      expect(find.text('The verification code is incorrect.'), findsOneWidget);
      expect(find.text('HOME'), findsNothing); // did not pop

      await tester.pumpWidget(const SizedBox()); // cancel the resend timer
    });
  });
}
