import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/sign_up_email_verify_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

/// A fake controller for the sign-up email-verify glue. `build()` returns
/// SignedOut so no cold-start restore runs; `verifyEmail` optionally throws and
/// `resendCode` returns the code lifetime (which the screen intentionally does
/// NOT use as the cooldown — D-695).
class _FakeController extends AuthController {
  _FakeController({this.verifyFailure});

  final AuthFailure? verifyFailure;
  bool verifyCalled = false;
  bool resendCalled = false;

  @override
  AuthState build() => const AuthStateSignedOut();

  @override
  Future<void> verifyEmail({
    required String email,
    required String code,
  }) async {
    verifyCalled = true;
    if (verifyFailure != null) {
      // AuthFailure is a sealed RESULT type: production returns it, never
      // throws. A fake repository throws it to drive the failure path a screen
      // handles, so it is deliberately neither an Exception nor an Error.
      // ignore: only_throw_errors
      throw verifyFailure!;
    }
  }

  @override
  Future<int> resendCode({required String email}) async {
    resendCalled = true;
    return 600; // the code lifetime the server returns (NOT the cooldown)
  }
}

/// In-memory prefs so the screen's D-742 `lastEmail` write on a successful
/// verify can be exercised (and asserted) without touching the platform.
class _FakePrefs implements SimfPrefsStorage {
  final Map<String, Object> _store = <String, Object>{};

  @override
  String? getString(String key) {
    final v = _store[key];
    return v is String ? v : null;
  }

  @override
  Future<bool> setString(String key, String value) async {
    _store[key] = value;
    return true;
  }

  @override
  bool? getBool(String key) => null;
  @override
  Future<bool> setBool(String key, bool value) async => true;
  @override
  double? getDouble(String key) => null;
  @override
  Future<bool> setDouble(String key, double value) async => true;
  @override
  int? getInt(String key) => null;
  @override
  Future<bool> setInt(String key, int value) async => true;
  @override
  Future<bool> remove(String key) async {
    _store.remove(key);
    return true;
  }
}

Future<void> _pump(
  WidgetTester tester,
  _FakeController controller, {
  SimfPrefsStorage? prefs,
}) async {
  final router = GoRouter(
    initialLocation: '/sign-up/otp',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.emailOtp,
        path: '/sign-up/otp',
        builder: (c, s) =>
            const SignUpEmailVerifyScreen(email: 'visitor@example.sa'),
      ),
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (c, s) => const Scaffold(body: Text('SIGN-IN')),
      ),
    ],
  );

  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        authControllerProvider.overrideWith(() => controller),
        simfPrefsStorageProvider.overrideWithValue(prefs ?? _FakePrefs()),
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
  // The screen starts a 1s periodic cooldown timer on entry (D-695), so pump a
  // frame rather than pumpAndSettle (which would never settle).
  await tester.pump();
}

bool _verifyEnabled(WidgetTester tester) {
  return tester
          .widget<FilledButton>(find.widgetWithText(FilledButton, 'Verify'))
          .onPressed !=
      null;
}

bool _resendEnabled(WidgetTester tester) {
  return tester
          .widget<TextButton>(find.widgetWithText(TextButton, 'Resend'))
          .onPressed !=
      null;
}

void main() {
  group('SignUpEmailVerifyScreen (Page 006)', () {
    testWidgets(
        'Verify is disabled until 6 digits, then verifies and routes '
        'to sign-in', (tester) async {
      final controller = _FakeController();
      final prefs = _FakePrefs();
      await _pump(tester, controller, prefs: prefs);

      expect(find.text('visitor@example.sa'), findsOneWidget);
      expect(_verifyEnabled(tester), isFalse);

      await tester.enterText(find.byType(TextField), '492700');
      await tester.pump();
      expect(_verifyEnabled(tester), isTrue);

      await tester.tap(find.widgetWithText(FilledButton, 'Verify'));
      await tester.pump(); // start the async verify
      await tester.pump(const Duration(milliseconds: 350)); // route transition

      expect(controller.verifyCalled, isTrue);
      expect(find.text('SIGN-IN'), findsOneWidget);
      // D-742 — a verified account prefills the just-registered address on the
      // sign-in screen (owner: login kept showing a stale/first-typed email).
      expect(prefs.getString(StorageKeys.lastEmail), 'visitor@example.sa');
    });

    testWidgets('a wrong code shows the inline error and clears the field',
        (tester) async {
      final controller = _FakeController(
        verifyFailure: const VerificationCodeInvalid(
          ApiFailure(
            code: 'AUTH_CODE_INVALID',
            message: 'The verification code is not correct.',
            httpStatus: 400,
          ),
        ),
      );
      await _pump(tester, controller);

      await tester.enterText(find.byType(TextField), '000000');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Verify'));
      await tester.pump(); // start the async verify
      await tester.pump(); // settle the error setState

      expect(
          find.text('The verification code is not correct.'), findsOneWidget,);
      expect(find.text('SIGN-IN'), findsNothing);
      expect(_verifyEnabled(tester), isFalse); // field was cleared

      // Cancel the on-entry cooldown timer (the screen stays mounted here).
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets(
        'the resend cooldown shows on entry and blocks resend until it '
        'elapses (D-695)', (tester) async {
      final controller = _FakeController();
      await _pump(tester, controller);

      // On entry the 2-minute countdown is visible and resend is disabled;
      // the reviewer wanted the countdown to appear before resend is allowed.
      // The D-364 design splits it into a label + mm:ss time.
      expect(find.text('Resend in'), findsOneWidget);
      expect(find.text('02:00'), findsOneWidget);
      expect(_resendEnabled(tester), isFalse);

      // Let the countdown run out; resend then becomes available.
      await tester.pump(const Duration(seconds: 120));
      expect(_resendEnabled(tester), isTrue);

      // Resend re-issues the code and restarts a fresh 2-minute cooldown; the
      // 600s code lifetime returned by resendCode is NOT used as the cooldown.
      await tester.tap(find.widgetWithText(TextButton, 'Resend'));
      await tester.pump();
      expect(controller.resendCalled, isTrue);
      expect(find.text('02:00'), findsOneWidget);

      // Dispose the tree so the cooldown Timer is cancelled (no pending timer).
      await tester.pumpWidget(const SizedBox());
    });
  });
}
