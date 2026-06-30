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

/// A fake controller for the sign-up email-verify glue. `build()` returns
/// SignedOut so no cold-start restore runs; `verifyEmail` optionally throws and
/// `resendCode` returns a configured cooldown.
class _FakeController extends AuthController {
  _FakeController({this.verifyFailure, this.resendSeconds = 60});

  final AuthFailure? verifyFailure;
  final int resendSeconds;
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
      throw verifyFailure!;
    }
  }

  @override
  Future<int> resendCode({required String email}) async {
    resendCalled = true;
    return resendSeconds;
  }
}

Future<void> _pump(WidgetTester tester, _FakeController controller) async {
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
    ProviderScope(
      overrides: <Override>[
        authControllerProvider.overrideWith(() => controller),
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
}

bool _verifyEnabled(WidgetTester tester) {
  return tester
          .widget<FilledButton>(find.widgetWithText(FilledButton, 'Verify'))
          .onPressed !=
      null;
}

void main() {
  group('SignUpEmailVerifyScreen (Page 006)', () {
    testWidgets('Verify is disabled until 6 digits, then verifies and routes '
        'to sign-in', (tester) async {
      final controller = _FakeController();
      await _pump(tester, controller);

      expect(find.text('visitor@example.sa'), findsOneWidget);
      expect(_verifyEnabled(tester), isFalse);

      await tester.enterText(find.byType(TextField), '492700');
      await tester.pump();
      expect(_verifyEnabled(tester), isTrue);

      await tester.tap(find.widgetWithText(FilledButton, 'Verify'));
      await tester.pumpAndSettle();

      expect(controller.verifyCalled, isTrue);
      expect(find.text('SIGN-IN'), findsOneWidget);
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
      await tester.pumpAndSettle();

      expect(find.text('The verification code is not correct.'), findsOneWidget);
      expect(find.text('SIGN-IN'), findsNothing);
      expect(_verifyEnabled(tester), isFalse); // field was cleared
    });

    testWidgets('Resend re-issues the code and starts the cooldown',
        (tester) async {
      final controller = _FakeController(resendSeconds: 45);
      await _pump(tester, controller);

      await tester.tap(find.widgetWithText(TextButton, 'Resend'));
      await tester.pump();

      expect(controller.resendCalled, isTrue);
      // The D-364 design splits the countdown into a label + mm:ss time.
      expect(find.text('Resend in'), findsOneWidget);
      expect(find.text('00:45'), findsOneWidget);

      // Dispose the tree so the cooldown Timer is cancelled (no pending timer).
      await tester.pumpWidget(const SizedBox());
    });
  });
}
