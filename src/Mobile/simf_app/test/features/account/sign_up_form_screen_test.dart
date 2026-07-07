import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/sign_up_form_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// A fake controller whose `signUp` records the call and optionally throws,
/// so the screen's validation → submit → navigation/error glue is testable
/// in isolation. `build()` returns SignedOut so no cold-start restore runs.
class _FakeSignUpController extends AuthController {
  _FakeSignUpController({this.failure});

  final AuthFailure? failure;
  bool signUpCalled = false;

  @override
  AuthState build() => const AuthStateSignedOut();

  @override
  Future<void> signUp({
    required String email,
    required String password,
    required String confirmPassword,
  }) async {
    signUpCalled = true;
    if (failure != null) {
      throw failure!;
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
  WidgetTester tester, {
  _FakeSignUpController? controller,
  _FakePrefs? prefs,
}) async {
  final fakeController = controller ?? _FakeSignUpController();
  final fakePrefs = prefs ?? _FakePrefs();
  final router = GoRouter(
    initialLocation: '/sign-up',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.signUpForm,
        path: '/sign-up',
        builder: (c, s) => const SignUpFormScreen(),
      ),
      GoRoute(
        name: RouteNames.emailOtp,
        path: '/sign-up/otp',
        builder: (c, s) =>
            Scaffold(body: Text('OTP email=${s.uri.queryParameters['email']}')),
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
        simfPrefsStorageProvider.overrideWithValue(fakePrefs),
        authControllerProvider.overrideWith(() => fakeController),
        localeControllerProvider.overrideWith(
          () => LocaleController(prefs: fakePrefs),
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
}

Future<void> _fill(
  WidgetTester tester, {
  required String email,
  required String password,
  required String confirm,
}) async {
  await tester.enterText(find.byType(TextFormField).at(0), email);
  await tester.enterText(find.byType(TextFormField).at(1), password);
  await tester.enterText(find.byType(TextFormField).at(2), confirm);
  await tester.pump();
}

Future<void> _tapCreate(WidgetTester tester) async {
  await tester.tap(find.widgetWithText(FilledButton, 'Create account'));
  await tester.pumpAndSettle();
}

void main() {
  group('SignUpFormScreen (Page 005 — KSA design, D-370)', () {
    testWidgets('valid input creates the account and routes to the email-OTP '
        'screen carrying the trimmed/lower-cased email', (tester) async {
      final controller = _FakeSignUpController();
      await _pump(tester, controller: controller);

      await _fill(
        tester,
        email: 'Visitor@Example.SA',
        password: 'Password1!',
        confirm: 'Password1!',
      );
      await _tapCreate(tester);

      expect(controller.signUpCalled, isTrue);
      expect(find.text('OTP email=visitor@example.sa'), findsOneWidget);
    });

    testWidgets('mismatched confirm shows the error and never calls sign-up',
        (tester) async {
      final controller = _FakeSignUpController();
      await _pump(tester, controller: controller);

      await _fill(
        tester,
        email: 'visitor@example.sa',
        password: 'Password1!',
        confirm: 'Password2!',
      );
      await _tapCreate(tester);

      expect(find.text('The passwords do not match.'), findsOneWidget);
      expect(controller.signUpCalled, isFalse);
      expect(find.textContaining('OTP email='), findsNothing);
    });

    testWidgets('an invalid email blocks submit', (tester) async {
      final controller = _FakeSignUpController();
      await _pump(tester, controller: controller);

      await _fill(
        tester,
        email: 'not-an-email',
        password: 'Password1!',
        confirm: 'Password1!',
      );
      await _tapCreate(tester);

      expect(find.text('Invalid email'), findsOneWidget);
      expect(controller.signUpCalled, isFalse);
    });

    testWidgets('a weak password blocks submit', (tester) async {
      final controller = _FakeSignUpController();
      await _pump(tester, controller: controller);

      await _fill(
        tester,
        email: 'visitor@example.sa',
        password: 'short',
        confirm: 'short',
      );
      await _tapCreate(tester);

      expect(find.textContaining('special character'), findsOneWidget);
      expect(controller.signUpCalled, isFalse);
    });

    testWidgets('a wire failure surfaces the message and keeps the form',
        (tester) async {
      final controller = _FakeSignUpController(
        failure: const ValidationFailed(
          ApiFailure(
            code: 'VALIDATION_FAILED',
            message: 'The request was invalid.',
            httpStatus: 400,
          ),
        ),
      );
      await _pump(tester, controller: controller);

      await _fill(
        tester,
        email: 'visitor@example.sa',
        password: 'Password1!',
        confirm: 'Password1!',
      );
      await _tapCreate(tester);

      expect(controller.signUpCalled, isTrue);
      expect(find.text('The request was invalid.'), findsOneWidget);
      expect(find.textContaining('OTP email='), findsNothing);
    });

    testWidgets('a validation failure surfaces the specific field reason, not '
        'the generic envelope message', (tester) async {
      // The server returns a generic top-level message with the real reason in
      // details[] — the app must show the specific reason so the user knows
      // what to fix (e.g. the failing password rule), not a useless generic.
      final controller = _FakeSignUpController(
        failure: const ValidationFailed(
          ApiFailure(
            code: 'VALIDATION_FAILED',
            message: 'One or more fields are invalid.',
            httpStatus: 400,
            details: <ApiResultErrorDetail>[
              ApiResultErrorDetail(
                field: 'password',
                message: 'Password must contain a special character.',
              ),
            ],
          ),
        ),
      );
      await _pump(tester, controller: controller);

      await _fill(
        tester,
        email: 'visitor@example.sa',
        password: 'Password1!',
        confirm: 'Password1!',
      );
      await _tapCreate(tester);

      expect(controller.signUpCalled, isTrue);
      expect(
        find.text('Password must contain a special character.'),
        findsOneWidget,
      );
      expect(find.text('One or more fields are invalid.'), findsNothing);
    });

    testWidgets('the Sign in link leaves the sign-up flow', (tester) async {
      await _pump(tester);

      await tester.ensureVisible(find.widgetWithText(TextButton, 'Sign in'));
      await tester.tap(find.widgetWithText(TextButton, 'Sign in'));
      await tester.pumpAndSettle();

      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets('the back chevron with no history falls back to sign-in '
        '(D-370)', (tester) async {
      await _pump(tester);

      await tester.tap(find.byKey(const ValueKey<String>('accountBack')));
      await tester.pumpAndSettle();

      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets('the language pill toggles and persists the language (D-370)',
        (tester) async {
      final prefs = _FakePrefs();
      await _pump(tester, prefs: prefs);

      // Empty prefs boot the controller in Arabic; the toggle flips to EN.
      await tester.tap(find.byKey(const ValueKey<String>('languageToggle')));
      await tester.pumpAndSettle();

      expect(prefs.getString(StorageKeys.preferredLanguage), equals('en'));
    });
  });
}
