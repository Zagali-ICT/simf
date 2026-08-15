import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/badge_password_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-738 — a fake AuthController for the badge password step: `build()` returns
/// SignedOut, and `signInWithBadge` either throws (wrong password) or sets the
/// AwaitingOtp state (2FA account). The signed-in success path is covered by
/// the controller unit test (it drives shared post-auth routing).
class _FakeController extends AuthController {
  _FakeController({this.failure, this.awaitingOtp = false});

  final AuthFailure? failure;
  final bool awaitingOtp;
  String? lastQrId;
  String? lastPassword;

  @override
  AuthState build() => const AuthStateSignedOut();

  @override
  Future<void> signInWithBadge({
    required String qrId,
    required String password,
    String? displayEmail,
    bool rememberSession = true,
  }) async {
    lastQrId = qrId;
    lastPassword = password;
    if (failure != null) {
      // AuthFailure is a sealed RESULT type: production returns it, never
      // throws. A fake repository throws it to drive the failure path a screen
      // handles, so it is deliberately neither an Exception nor an Error.
      // ignore: only_throw_errors
      throw failure!;
    }
    if (awaitingOtp) {
      state = AuthStateAwaitingOtp('otp-tok', email: displayEmail);
    }
  }
}

Future<void> _pump(
  WidgetTester tester,
  _FakeController controller, {
  String locale = 'en',
  String? name = 'Khalid',
  String? masked = 'k***@example.com',
}) async {
  final router = GoRouter(
    initialLocation: '/badge-password',
    routes: <RouteBase>[
      GoRoute(
        path: '/badge-password',
        builder: (c, s) => BadgePasswordScreen(
            qrId: 'QR1', displayName: name, maskedEmail: masked,),
      ),
      GoRoute(
        name: RouteNames.verifyOtp,
        path: '/auth/verify-otp',
        builder: (c, s) => const Scaffold(body: Text('OTP-STUB')),
      ),
      GoRoute(
        name: RouteNames.forgotPassword,
        path: '/auth/forgot-password',
        builder: (c, s) => const Scaffold(body: Text('FORGOT-STUB')),
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
        locale: Locale(locale),
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

void main() {
  group('BadgePasswordScreen (D-738)', () {
    testWidgets('renders the resolved name + masked email + password field',
        (tester) async {
      await _pump(tester, _FakeController());

      expect(find.text('Welcome, Khalid'), findsOneWidget);
      expect(find.textContaining('k***@example.com'), findsOneWidget);
      expect(find.byType(TextFormField), findsOneWidget);
      expect(
          find.widgetWithText(TextButton, 'Forgot password?'), findsOneWidget,);
    });

    testWidgets('renders in Arabic', (tester) async {
      await _pump(tester, _FakeController(), locale: 'ar');
      expect(find.text('مرحبًا Khalid'), findsOneWidget);
    });

    testWidgets('a wrong password shows the inline error and clears the field',
        (tester) async {
      final controller = _FakeController(
        failure: const UnknownAuthFailure(
          ApiFailure(
            code: 'AUTH_INVALID_CREDENTIALS',
            message: 'The email address or password is not correct.',
            httpStatus: 401,
          ),
        ),
      );
      await _pump(tester, controller);

      await tester.enterText(find.byType(TextFormField), 'wrongpw');
      await tester.pump(); // let onChanged enable the button
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(controller.lastQrId, 'QR1');
      expect(
        find.text('The email address or password is not correct.'),
        findsOneWidget,
      );
    });

    testWidgets('a 2FA account continues to the OTP screen', (tester) async {
      await _pump(tester, _FakeController(awaitingOtp: true));

      await tester.enterText(find.byType(TextFormField), 'Passw0rd1!');
      await tester.pump(); // let onChanged enable the button
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('OTP-STUB'), findsOneWidget);
    });
  });
}
