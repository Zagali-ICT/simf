import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/reset_password_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Widget tests for the reset-password form (Page 003 — L-6). They prove the
/// per-field validation gate added with the form conversion: a blank/short code,
/// a policy-failing new password, and a non-matching confirm each show the
/// field's inline error and block the network call; a fully valid form calls
/// `resetPassword` once and routes back to sign-in.
class _FakeAuthRepo implements AuthRepository {
  int resetCalls = 0;

  @override
  Future<void> resetPassword({
    required String email,
    required String code,
    required String newPassword,
    required String confirmPassword,
  }) async {
    resetCalls++;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
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

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: CurrentUser(
        id: 'u1',
        email: 'visitor@example.sa',
        displayName: 'Raed',
        appRole: AppRole.visitor,
        preferredLanguage: PreferredLanguage.fromJson('en'),
        registrationStatus: RegistrationStatus.approved,
      ),
    );

/// Fake auth controller — starts signed-in or signed-out and records whether
/// the reset flow signed out (the D-659 profile-reset clean-up).
class _FakeAuthController extends AuthController {
  _FakeAuthController({required this.signedIn});

  final bool signedIn;
  int signOutCalls = 0;

  @override
  AuthState build() =>
      signedIn ? AuthStateSignedIn(_session()) : const AuthStateSignedOut();

  @override
  Future<void> signOut() async {
    signOutCalls++;
    state = const AuthStateSignedOut();
  }
}

Future<void> _pump(
  WidgetTester tester,
  _FakeAuthRepo repo,
  _FakePrefs prefs, {
  _FakeAuthController? auth,
}) async {
  final authController = auth ?? _FakeAuthController(signedIn: false);
  final router = GoRouter(
    initialLocation: '/reset',
    routes: <RouteBase>[
      GoRoute(
        path: '/reset',
        builder: (c, s) =>
            const ResetPasswordScreen(email: 'visitor@example.sa'),
      ),
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (c, s) => const Scaffold(body: Text('SIGN-IN')),
      ),
      GoRoute(
        name: RouteNames.forgotPassword,
        path: '/auth/forgot-password',
        builder: (c, s) => const Scaffold(body: Text('FORGOT')),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        authRepositoryProvider.overrideWithValue(repo),
        simfPrefsStorageProvider.overrideWithValue(prefs),
        authControllerProvider.overrideWith(() => authController),
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

/// Fills the three fields (code / password / confirm) in order.
Future<void> _fill(
  WidgetTester tester, {
  required String code,
  required String password,
  required String confirm,
}) async {
  await tester.enterText(find.byType(TextFormField).at(0), code);
  await tester.enterText(find.byType(TextFormField).at(1), password);
  await tester.enterText(find.byType(TextFormField).at(2), confirm);
  await tester.pump();
}

Future<void> _tapReset(WidgetTester tester) async {
  final button = find.widgetWithText(FilledButton, 'Reset');
  await tester.ensureVisible(button);
  await tester.pumpAndSettle();
  await tester.tap(button);
  await tester.pumpAndSettle();
}

void main() {
  group('ResetPasswordScreen — field validation gate', () {
    testWidgets('a valid form resets the password and routes to sign-in',
        (tester) async {
      final repo = _FakeAuthRepo();
      await _pump(tester, repo, _FakePrefs());

      await _fill(
        tester,
        code: '123456',
        password: 'Password1!',
        confirm: 'Password1!',
      );
      await _tapReset(tester);

      expect(repo.resetCalls, 1);
      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets('a short OTP code shows the required error and blocks submit',
        (tester) async {
      final repo = _FakeAuthRepo();
      await _pump(tester, repo, _FakePrefs());

      // 3 digits — fails the length==6 rule.
      await _fill(
        tester,
        code: '123',
        password: 'Password1!',
        confirm: 'Password1!',
      );
      await _tapReset(tester);

      expect(find.text('This field is required'), findsOneWidget);
      expect(repo.resetCalls, 0);
      expect(find.text('SIGN-IN'), findsNothing);
    });

    testWidgets('a policy-failing new password shows the policy error and '
        'blocks submit', (tester) async {
      final repo = _FakeAuthRepo();
      await _pump(tester, repo, _FakePrefs());

      // "short" — under 8 chars and no digit, so the policy fails. Confirm is
      // made to match so only the password rule is under test.
      await _fill(
        tester,
        code: '123456',
        password: 'short',
        confirm: 'short',
      );
      await _tapReset(tester);

      expect(find.textContaining('special character'), findsOneWidget);
      expect(repo.resetCalls, 0);
      expect(find.text('SIGN-IN'), findsNothing);
    });

    testWidgets('a non-matching confirm shows the mismatch error and blocks '
        'submit', (tester) async {
      final repo = _FakeAuthRepo();
      await _pump(tester, repo, _FakePrefs());

      await _fill(
        tester,
        code: '123456',
        password: 'Password1!',
        confirm: 'Password2!',
      );
      await _tapReset(tester);

      expect(find.text('The passwords do not match.'), findsOneWidget);
      expect(repo.resetCalls, 0);
      expect(find.text('SIGN-IN'), findsNothing);
    });

    testWidgets(
        'a signed-in profile reset signs out then routes to sign-in (D-659)',
        (tester) async {
      final repo = _FakeAuthRepo();
      final auth = _FakeAuthController(signedIn: true);
      await _pump(tester, repo, _FakePrefs(), auth: auth);

      await _fill(
        tester,
        code: '123456',
        password: 'Password1!',
        confirm: 'Password1!',
      );
      await _tapReset(tester);

      expect(repo.resetCalls, 1);
      // Signed-in reset clears the stale session before landing on sign-in.
      expect(auth.signOutCalls, 1);
      expect(find.text('SIGN-IN'), findsOneWidget);
    });
  });
}
