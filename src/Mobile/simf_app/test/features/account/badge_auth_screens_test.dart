import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/badge_activation_screen.dart';
import 'package:simf_app/features/account/badge_sign_in_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Part B (D-430) — widget tests for the badge-QR sign-in / activation screens:
/// the resolve branch (has-password → sign-in; needs-email → activation) and
/// the activation email-step render. The camera is off so the manual-entry path
/// drives the resolve (no native plugin in the test environment).
class _FakeAuthRepo implements AuthRepository {
  _FakeAuthRepo(this._resolve);

  final ({
    bool found,
    bool hasPassword,
    String? displayName,
    bool needsEmail,
    String? maskedEmail
  }) _resolve;

  int startCalls = 0;
  int completeCalls = 0;

  @override
  Future<
      ({
        bool found,
        bool hasPassword,
        String? displayName,
        bool needsEmail,
        String? maskedEmail
      })> resolveBadge({required String qrId}) async => _resolve;

  @override
  Future<({String maskedEmail, int codeExpiresInSeconds})>
      badgeActivationStart({
    required String qrId,
    String? email,
  }) async {
    startCalls++;
    return (maskedEmail: 'k***@example.com', codeExpiresInSeconds: 600);
  }

  @override
  Future<void> badgeActivationComplete({
    required String qrId,
    required String code,
    required String newPassword,
    required String confirmPassword,
  }) async {
    completeCalls++;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
}

/// Mounts the activation screen directly (skipping the badge-resolve hop) so
/// the validators can be driven on each step. [needsEmail] selects the email
/// step (true) or the auto-started code+password step (false).
Widget _activationHost({
  required AuthRepository repo,
  required bool needsEmail,
}) {
  final router = GoRouter(
    initialLocation: '/activate',
    routes: <RouteBase>[
      GoRoute(
        path: '/activate',
        builder: (c, s) =>
            BadgeActivationScreen(qrId: 'QR1', needsEmail: needsEmail),
      ),
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (c, s) => const Scaffold(body: Text('SIGN-IN-STUB')),
      ),
    ],
  );
  return ProviderScope(
    overrides: <Override>[authRepositoryProvider.overrideWithValue(repo)],
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
  );
}

Widget _host(
    {required Widget Function(GoRouterState) home,
    required AuthRepository repo,}) {
  final router = GoRouter(
    initialLocation: '/badge',
    routes: <RouteBase>[
      GoRoute(path: '/badge', builder: (c, s) => home(s)),
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (c, s) => const Scaffold(body: Text('SIGN-IN-STUB')),
      ),
      GoRoute(
        name: RouteNames.badgePassword,
        path: '/auth/badge-password',
        builder: (c, s) => Scaffold(
          body: Text('BADGE-PW:${s.uri.queryParameters['name']}'),
        ),
      ),
      GoRoute(
        name: RouteNames.badgeActivation,
        path: '/auth/badge-activation',
        builder: (c, s) => BadgeActivationScreen(
          qrId: s.uri.queryParameters['qrId'] ?? '',
          needsEmail: s.uri.queryParameters['needsEmail'] == '1',
          maskedEmail: s.uri.queryParameters['masked'],
        ),
      ),
    ],
  );
  return ProviderScope(
    overrides: <Override>[authRepositoryProvider.overrideWithValue(repo)],
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
  );
}

void main() {
  testWidgets(
      'manual-entry resolve of a has-password badge routes to the '
      'password step with the resolved name (D-738)', (tester) async {
    await tester.pumpWidget(
      _host(
        home: (_) => const BadgeSignInScreen(enableCamera: false),
        repo: _FakeAuthRepo(
          (
            found: true,
            hasPassword: true,
            displayName: 'Khalid',
            needsEmail: false,
            maskedEmail: null,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).first, 'ABCDEFGH2345');
    await tester.ensureVisible(find.text('Continue'));
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    // Now goes to the password-completion step (not the blank sign-in form),
    // carrying the resolved name.
    expect(find.text('BADGE-PW:Khalid'), findsOneWidget);
  });

  testWidgets('manual-entry resolve of a passwordless badge opens activation',
      (tester) async {
    await tester.pumpWidget(
      _host(
        home: (_) => const BadgeSignInScreen(enableCamera: false),
        repo: _FakeAuthRepo(
          (
            found: true,
            hasPassword: false,
            displayName: 'Khalid',
            needsEmail: true,
            maskedEmail: null,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).first, 'ABCDEFGH2345');
    await tester.ensureVisible(find.text('Continue'));
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    // The activation screen (email step) is now shown.
    expect(find.text('Activate your account'), findsOneWidget);
    expect(find.text('Send code'), findsOneWidget);
  });

  testWidgets('unknown badge shows the not-recognised message', (tester) async {
    await tester.pumpWidget(
      _host(
        home: (_) => const BadgeSignInScreen(enableCamera: false),
        repo: _FakeAuthRepo(
          (
            found: false,
            hasPassword: false,
            displayName: null,
            needsEmail: false,
            maskedEmail: null,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).first, 'ZZZZZZZZZZZZ');
    await tester.ensureVisible(find.text('Continue'));
    await tester.tap(find.text('Continue'));
    await tester.pump(); // let the SnackBar appear

    expect(find.text('The badge was not recognised.'), findsOneWidget);
  });

  testWidgets('activation email step: a malformed email blocks the start call',
      (tester) async {
    final repo = _FakeAuthRepo(
      (
        found: true,
        hasPassword: false,
        displayName: 'Khalid',
        needsEmail: true,
        maskedEmail: null,
      ),
    );
    await tester.pumpWidget(_activationHost(repo: repo, needsEmail: true));
    await tester.pumpAndSettle();

    // A non-empty but malformed address enables the button but fails the
    // validator — the network start is never reached.
    await tester.enterText(find.byType(TextFormField), 'not-an-email');
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Send code'));
    await tester.tap(find.text('Send code'));
    await tester.pumpAndSettle();

    expect(find.text('Invalid email'), findsOneWidget);
    expect(repo.startCalls, 0);
  });

  testWidgets('activation email step: a valid email passes and sends the code',
      (tester) async {
    final repo = _FakeAuthRepo(
      (
        found: true,
        hasPassword: false,
        displayName: 'Khalid',
        needsEmail: true,
        maskedEmail: null,
      ),
    );
    await tester.pumpWidget(_activationHost(repo: repo, needsEmail: true));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextFormField), 'khalid@example.com');
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Send code'));
    await tester.tap(find.text('Send code'));
    await tester.pumpAndSettle();

    expect(find.text('Invalid email'), findsNothing);
    expect(repo.startCalls, 1);
    // The code + password step is now shown.
    expect(find.text('Activate & set password'), findsOneWidget);
  });

  testWidgets('activation code step: a weak password blocks the complete call',
      (tester) async {
    final repo = _FakeAuthRepo(
      (
        found: true,
        hasPassword: false,
        displayName: 'Khalid',
        needsEmail: false,
        maskedEmail: 'k***@example.com',
      ),
    );
    // needsEmail:false auto-starts on open, landing on the code step.
    await tester.pumpWidget(_activationHost(repo: repo, needsEmail: false));
    await tester.pumpAndSettle();
    expect(repo.startCalls, 1);

    final fields = find.byType(TextFormField);
    await tester.enterText(fields.at(0), '123456'); // code
    await tester.enterText(fields.at(1), 'short'); // weak password
    await tester.enterText(fields.at(2), 'short'); // matching confirm
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.text('Activate & set password'));
    await tester.tap(find.text('Activate & set password'));
    await tester.pumpAndSettle();

    expect(
      find.textContaining('special character'),
      findsOneWidget,
    );
    expect(repo.completeCalls, 0);
  });

  testWidgets('activation code step: a mismatched confirm blocks complete',
      (tester) async {
    final repo = _FakeAuthRepo(
      (
        found: true,
        hasPassword: false,
        displayName: 'Khalid',
        needsEmail: false,
        maskedEmail: 'k***@example.com',
      ),
    );
    await tester.pumpWidget(_activationHost(repo: repo, needsEmail: false));
    await tester.pumpAndSettle();

    final fields = find.byType(TextFormField);
    await tester.enterText(fields.at(0), '123456');
    await tester.enterText(fields.at(1), 'Passw0rd1!');
    await tester.enterText(fields.at(2), 'Passw0rd2!'); // differs
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.text('Activate & set password'));
    await tester.tap(find.text('Activate & set password'));
    await tester.pumpAndSettle();

    expect(find.text('The passwords do not match.'), findsOneWidget);
    expect(repo.completeCalls, 0);
  });

  testWidgets('activation code step: valid input passes and completes',
      (tester) async {
    final repo = _FakeAuthRepo(
      (
        found: true,
        hasPassword: false,
        displayName: 'Khalid',
        needsEmail: false,
        maskedEmail: 'k***@example.com',
      ),
    );
    await tester.pumpWidget(_activationHost(repo: repo, needsEmail: false));
    await tester.pumpAndSettle();

    final fields = find.byType(TextFormField);
    await tester.enterText(fields.at(0), '123456');
    await tester.enterText(fields.at(1), 'Passw0rd1!');
    await tester.enterText(fields.at(2), 'Passw0rd1!');
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.text('Activate & set password'));
    await tester.tap(find.text('Activate & set password'));
    await tester.pumpAndSettle();

    expect(repo.completeCalls, 1);
  });
}
