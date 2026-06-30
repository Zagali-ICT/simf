import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/email_otp_verify_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Widget tests for the email-OTP second-factor screen (Page 003 2FA, frame
/// 758:2616). The screen runs a 1s periodic resend timer, so these pump frames
/// rather than `pumpAndSettle` (which would never settle); the timer is
/// cancelled when the screen is disposed at the end of each test.
class _FakeAuthController extends AuthController {
  static const String email = 'r.alsalem@xxx.sa';
  String? verifiedCode;
  bool resendCalled = false;

  @override
  AuthState build() => const AuthStateAwaitingOtp('otp-token', email: email);

  @override
  Future<void> verifyOtp({required String code}) async {
    verifiedCode = code;
    // Throw after capturing so the test stays on the screen (a success would
    // route home via routeAfterAuth + the biometric-enrolment offer).
    throw const InvalidCredentials(
      ApiFailure(
        code: ApiErrorCodes.authInvalidCredentials,
        message: 'Incorrect code.',
        httpStatus: 401,
      ),
    );
  }

  @override
  Future<int> resendOtp() async {
    resendCalled = true;
    return 60;
  }
}

Future<void> _pump(WidgetTester tester, _FakeAuthController controller) async {
  final router = GoRouter(
    initialLocation: '/auth/verify-otp',
    routes: <RouteBase>[
      GoRoute(
        path: '/auth/verify-otp',
        name: RouteNames.verifyOtp,
        builder: (_, __) => const EmailOtpVerifyScreen(),
      ),
      GoRoute(
        path: '/sign-in',
        name: RouteNames.signIn,
        builder: (_, __) => const Scaffold(body: Text('SIGN-IN')),
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
  await tester.pump();
}

FilledButton _verifyButton(WidgetTester tester) =>
    tester.widget<FilledButton>(find.byType(FilledButton));

void main() {
  group('EmailOtpVerifyScreen (Page 003 2FA — frame 758:2616)', () {
    testWidgets('renders the recipient email, the six boxes and the controls',
        (tester) async {
      await _pump(tester, _FakeAuthController());

      expect(find.text('r.alsalem@xxx.sa'), findsOneWidget);
      expect(find.byType(TextField), findsOneWidget); // the capture field
      expect(find.byType(FilledButton), findsOneWidget); // verify CTA
      expect(find.byType(IconButton), findsOneWidget); // back chevron
    });

    testWidgets('verify is disabled until six digits are entered',
        (tester) async {
      await _pump(tester, _FakeAuthController());

      expect(_verifyButton(tester).onPressed, isNull);

      await tester.enterText(find.byType(TextField), '12345');
      await tester.pump();
      expect(_verifyButton(tester).onPressed, isNull);

      await tester.enterText(find.byType(TextField), '123456');
      await tester.pump();
      expect(_verifyButton(tester).onPressed, isNotNull);
    });

    testWidgets('tapping verify with six digits calls verifyOtp with the code',
        (tester) async {
      final controller = _FakeAuthController();
      await _pump(tester, controller);

      await tester.enterText(find.byType(TextField), '123456');
      await tester.pump();
      await tester.tap(find.byType(FilledButton));
      await tester.pump(); // start the async verify
      await tester.pump(); // settle the error setState

      expect(controller.verifiedCode, '123456');
    });

    testWidgets('the back chevron with no history falls back to sign-in',
        (tester) async {
      await _pump(tester, _FakeAuthController());

      await tester.tap(find.byType(IconButton));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 350));

      expect(find.text('SIGN-IN'), findsOneWidget);
    });
  });
}
