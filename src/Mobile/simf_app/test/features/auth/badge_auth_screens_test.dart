import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/auth/badge_activation_screen.dart';
import 'package:simf_app/features/auth/badge_sign_in_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Part B (D-430) — widget tests for the badge-QR sign-in / activation screens:
/// the resolve branch (has-password → sign-in; needs-email → activation) and the
/// activation email-step render. The camera is off so the manual-entry path
/// drives the resolve (no native plugin in the test environment).
class _FakeAuthRepo implements AuthRepository {
  _FakeAuthRepo(this._resolve);

  final ({bool found, bool hasPassword, String? displayName, bool needsEmail, String? maskedEmail})
      _resolve;

  @override
  Future<({bool found, bool hasPassword, String? displayName, bool needsEmail, String? maskedEmail})>
      resolveBadge({required String qrId}) async => _resolve;

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
}

Widget _host({required Widget Function(GoRouterState) home, required AuthRepository repo}) {
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
  testWidgets('manual-entry resolve of a has-password badge routes to sign-in',
      (tester) async {
    await tester.pumpWidget(_host(
      home: (_) => const BadgeSignInScreen(enableCamera: false),
      repo: _FakeAuthRepo((
        found: true, hasPassword: true, displayName: 'Khalid',
        needsEmail: false, maskedEmail: null,
      ),),
    ),);
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).first, 'ABCDEFGH2345');
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    expect(find.text('SIGN-IN-STUB'), findsOneWidget);
  });

  testWidgets('manual-entry resolve of a passwordless badge opens activation',
      (tester) async {
    await tester.pumpWidget(_host(
      home: (_) => const BadgeSignInScreen(enableCamera: false),
      repo: _FakeAuthRepo((
        found: true, hasPassword: false, displayName: 'Khalid',
        needsEmail: true, maskedEmail: null,
      ),),
    ),);
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).first, 'ABCDEFGH2345');
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    // The activation screen (email step) is now shown.
    expect(find.text('Activate your account'), findsOneWidget);
    expect(find.text('Send code'), findsOneWidget);
  });

  testWidgets('unknown badge shows the not-recognised message', (tester) async {
    await tester.pumpWidget(_host(
      home: (_) => const BadgeSignInScreen(enableCamera: false),
      repo: _FakeAuthRepo((
        found: false, hasPassword: false, displayName: null,
        needsEmail: false, maskedEmail: null,
      ),),
    ),);
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).first, 'ZZZZZZZZZZZZ');
    await tester.tap(find.text('Continue'));
    await tester.pump(); // let the SnackBar appear

    expect(find.text('The badge was not recognised.'), findsOneWidget);
  });
}
