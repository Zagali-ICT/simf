import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/auth/biometric_auth.dart';
import 'package:simf_app/features/auth/sign_in_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

enum _Outcome { success, otp, invalid }

/// A fake controller whose `signIn` transitions to a configured outcome,
/// so the screen's UI → navigation/error glue can be tested in isolation
/// (same harness as sign_in_screen_test.dart).
class _FakeAuthController extends AuthController {
  _FakeAuthController(this.outcome, {this.profileComplete = true});

  final _Outcome outcome;

  /// D-374 — the completeness flag rides on the signed-in session
  /// (`/users/me` → `profileComplete`); the old profile probe is gone.
  final bool profileComplete;

  @override
  AuthState build() => const AuthStateSignedOut();

  @override
  Future<void> signIn({required String email, required String password}) async {
    switch (outcome) {
      case _Outcome.success:
        state = AuthStateSignedIn(
          Session(
            accessToken: 'A',
            refreshToken: 'R',
            accessTokenExpiresAt: DateTime.now().add(const Duration(hours: 1)),
            user: CurrentUser(
              id: 'u1',
              email: email,
              displayName: 'Visitor',
              appRole: AppRole.visitor,
              preferredLanguage: PreferredLanguage.fromJson('ar'),
              registrationStatus: RegistrationStatus.approved,
              profileComplete: profileComplete,
            ),
          ),
        );
      case _Outcome.otp:
        state = const AuthStateAwaitingOtp('otp-token');
      case _Outcome.invalid:
        throw const InvalidCredentials(
          ApiFailure(
            code: ApiErrorCodes.authInvalidCredentials,
            message: 'Incorrect email or password.',
            httpStatus: 401,
          ),
        );
    }
  }
}

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
  _Outcome outcome,
  _FakePrefs prefs, {
  bool profileComplete = true,
  bool biometricAvailable = false,
}) async {
  final router = GoRouter(
    initialLocation: '/sign-in',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (c, s) => const SignInScreen(),
      ),
      GoRoute(
        name: RouteNames.home,
        path: '/',
        builder: (c, s) => const Scaffold(body: Text('HOME')),
      ),
      GoRoute(
        name: RouteNames.verifyOtp,
        path: '/auth/verify-otp',
        builder: (c, s) => const Scaffold(body: Text('OTP-SCREEN')),
      ),
      GoRoute(
        name: RouteNames.forgotPassword,
        path: '/auth/forgot-password',
        builder: (c, s) => const Scaffold(body: Text('FORGOT')),
      ),
      GoRoute(
        name: RouteNames.signUpForm,
        path: '/sign-up',
        builder: (c, s) => const Scaffold(body: Text('SIGN-UP')),
      ),
      GoRoute(
        name: RouteNames.signUpVisitor,
        path: '/sign-up/visitor',
        builder: (c, s) => const Scaffold(body: Text('PROFILE')),
      ),
      GoRoute(
        name: RouteNames.onboarding,
        path: '/onboarding',
        builder: (c, s) => const Scaffold(body: Text('ONBOARDING')),
      ),
      GoRoute(
        name: RouteNames.guestMode,
        path: '/guest',
        builder: (c, s) => const Scaffold(body: Text('GUEST')),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfPrefsStorageProvider.overrideWithValue(prefs),
        authControllerProvider.overrideWith(
          () => _FakeAuthController(outcome, profileComplete: profileComplete),
        ),
        localeControllerProvider.overrideWith(
          () => LocaleController(prefs: prefs),
        ),
        // The Face-ID button is gated on real device biometric availability;
        // pin it so the button's visibility is deterministic in the test.
        biometricAvailableProvider
            .overrideWith((ref) async => biometricAvailable),
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

Future<void> _enterCreds(WidgetTester tester) async {
  await tester.enterText(find.byType(TextField).at(0), 'visitor@example.sa');
  await tester.enterText(find.byType(TextField).at(1), 'Password1');
  await tester.pump();
}

void main() {
  group('SignInScreen (Page 003 — KSA design, D-360)', () {
    testWidgets('successful sign-in with a complete profile routes home and '
        'stores the email (remember-me default on)', (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.success, prefs);

      await _enterCreds(tester);
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('HOME'), findsOneWidget);
      expect(
        prefs.getString(StorageKeys.lastEmail),
        equals('visitor@example.sa'),
      );
    });

    testWidgets('the Face-ID button is hidden when the device has no biometric',
        (tester) async {
      await _pump(tester, _Outcome.success, _FakePrefs());
      // No usable biometric (sensorless device) → the Face-ID sign-in button
      // is not rendered at all (it can do nothing there).
      expect(find.text('Sign in with Face ID'), findsNothing);
    });

    testWidgets('the Face-ID button shows when a biometric is available',
        (tester) async {
      await _pump(
        tester,
        _Outcome.success,
        _FakePrefs(),
        biometricAvailable: true,
      );
      expect(find.text('Sign in with Face ID'), findsOneWidget);
    });

    testWidgets('unchecking remember-me skips storing the email',
        (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.success, prefs);

      await _enterCreds(tester);
      await tester.tap(find.byType(Checkbox));
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('HOME'), findsOneWidget);
      expect(prefs.getString(StorageKeys.lastEmail), isNull);
    });

    testWidgets('unchecking remember-me forgets a previously remembered email',
        (tester) async {
      final prefs = _FakePrefs();
      // A prior remembered sign-in left an address stored (and pre-filling).
      await prefs.setString(StorageKeys.lastEmail, 'old@example.sa');
      await _pump(tester, _Outcome.success, prefs);

      await _enterCreds(tester);
      await tester.tap(find.byType(Checkbox));
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('HOME'), findsOneWidget);
      // The opt-out is honoured in both directions: the old email is cleared.
      expect(prefs.getString(StorageKeys.lastEmail), isNull);
    });

    testWidgets('re-checking remember-me re-arms the email store',
        (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.success, prefs);

      await _enterCreds(tester);
      // Default is on → toggle off, then back on: the store must re-arm, not
      // stay disabled after a prior opt-out.
      await tester.tap(find.byType(Checkbox));
      await tester.pump();
      await tester.tap(find.byType(Checkbox));
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('HOME'), findsOneWidget);
      expect(
        prefs.getString(StorageKeys.lastEmail),
        equals('visitor@example.sa'),
      );
    });

    testWidgets('successful sign-in with an incomplete profile routes to the '
        'visitor profile screen (Page_007 auto-route)', (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.success, prefs, profileComplete: false);

      await _enterCreds(tester);
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('PROFILE'), findsOneWidget);
      expect(find.text('HOME'), findsNothing);
    });

    testWidgets('a 2FA account routes to the email-OTP screen', (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.otp, prefs);

      await _enterCreds(tester);
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('OTP-SCREEN'), findsOneWidget);
    });

    testWidgets('invalid credentials show the error and stay on the screen',
        (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.invalid, prefs);

      await _enterCreds(tester);
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('Incorrect email or password.'), findsOneWidget);
      expect(find.text('HOME'), findsNothing);
    });

    testWidgets('the email field is pre-filled from the last sign-in',
        (tester) async {
      final prefs = _FakePrefs();
      await prefs.setString(StorageKeys.lastEmail, 'prefilled@example.sa');
      await _pump(tester, _Outcome.success, prefs);

      expect(find.text('prefilled@example.sa'), findsOneWidget);
    });

    testWidgets('the create-account link opens the sign-up form',
        (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.success, prefs);

      await tester.tap(find.text('Create account'));
      await tester.pumpAndSettle();

      expect(find.text('SIGN-UP'), findsOneWidget);
    });

    testWidgets('the forgot-password link opens the reset flow',
        (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.success, prefs);

      await tester.tap(find.text('Forgot password?'));
      await tester.pumpAndSettle();

      expect(find.text('FORGOT'), findsOneWidget);
    });

    testWidgets('the back chevron with no history falls back to onboarding',
        (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.success, prefs);

      // Back is the first IconButton (top row); the icon is now an exact SVG.
      await tester.tap(find.byType(IconButton).first);
      await tester.pumpAndSettle();

      expect(find.text('ONBOARDING'), findsOneWidget);
    });

    testWidgets('the guest link opens the guest screen (D-363)',
        (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.success, prefs);

      await tester.ensureVisible(find.text('Enter as guest'));
      await tester.tap(find.text('Enter as guest'));
      await tester.pumpAndSettle();

      expect(find.text('GUEST'), findsOneWidget);
    });

    testWidgets('the globe button toggles and persists the language (D-363)',
        (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, _Outcome.success, prefs);

      // Empty prefs boot the controller in Arabic; the toggle flips to EN.
      // The globe is the second IconButton (top row); the icon is now an SVG.
      await tester.tap(find.byType(IconButton).at(1));
      await tester.pumpAndSettle();

      expect(prefs.getString(StorageKeys.preferredLanguage), equals('en'));
    });
  });
}
