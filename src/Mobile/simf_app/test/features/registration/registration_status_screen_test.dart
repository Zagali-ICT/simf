import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/registration/registration_status_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

CurrentUser _user(RegistrationStatus status) => CurrentUser(
      id: 'u1',
      email: 'visitor@example.sa',
      displayName: 'Visitor',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('ar'),
      registrationStatus: status,
    );

Session _session(RegistrationStatus status) => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _user(status),
    );

/// A signed-in fake controller whose `refreshCurrentUser` returns a configured
/// status (or throws), so the screen's state machine is testable in isolation.
class _FakeAuthController extends AuthController {
  _FakeAuthController({required this.status, this.fail = false});

  final RegistrationStatus status;
  final bool fail;
  int refreshCalls = 0;
  bool signedOut = false;

  @override
  AuthState build() => AuthStateSignedIn(_session(status));

  @override
  Future<CurrentUser> refreshCurrentUser() async {
    refreshCalls++;
    if (fail) {
      throw const NetworkUnavailable(
        ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'offline'),
      );
    }
    return _user(status);
  }

  @override
  Future<void> signOut() async {
    signedOut = true;
  }
}

Future<void> _pump(WidgetTester tester, _FakeAuthController controller) async {
  final router = GoRouter(
    initialLocation: '/registration/status',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.registrationStatus,
        path: '/registration/status',
        builder: (c, s) => const RegistrationStatusScreen(),
      ),
      GoRoute(
        name: RouteNames.home,
        path: '/',
        builder: (c, s) => const Scaffold(body: Text('HOME')),
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

void main() {
  group('RegistrationStatusScreen (Page 011)', () {
    testWidgets('pending shows the under-review state + Re-check + Go to home',
        (tester) async {
      await _pump(
        tester,
        _FakeAuthController(status: RegistrationStatus.pending),
      );

      expect(find.text('Your account is under review'), findsOneWidget);
      expect(find.text('Re-check'), findsOneWidget);
      // A non-approved account gets an explicit way back home (D-666).
      expect(find.text('Go to home'), findsOneWidget);
    });

    testWidgets('pending — "Go to home" routes to the home screen',
        (tester) async {
      await _pump(
        tester,
        _FakeAuthController(status: RegistrationStatus.pending),
      );

      await tester.tap(find.text('Go to home'));
      await tester.pumpAndSettle();
      expect(find.text('HOME'), findsOneWidget);
    });

    testWidgets('approved shows Continue, which routes home', (tester) async {
      final controller =
          _FakeAuthController(status: RegistrationStatus.approved);
      await _pump(tester, controller);

      expect(find.text('Your account is approved'), findsOneWidget);
      final continueButton = find.text('Continue');
      expect(continueButton, findsOneWidget);
      // Approved reaches home via Continue, so there is no separate Go-to-home.
      expect(find.text('Go to home'), findsNothing);

      await tester.tap(continueButton);
      await tester.pumpAndSettle();
      expect(find.text('HOME'), findsOneWidget);
    });

    testWidgets('rejected shows the declined state with no Continue',
        (tester) async {
      await _pump(
        tester,
        _FakeAuthController(status: RegistrationStatus.rejected),
      );

      expect(find.text('Your account was not approved'), findsOneWidget);
      expect(find.text('Continue'), findsNothing);
      // Even with no primary action, a rejected account can return home (D-666).
      expect(find.text('Go to home'), findsOneWidget);
    });

    testWidgets('a load failure shows the error + retry, which re-fetches',
        (tester) async {
      final controller = _FakeAuthController(
        status: RegistrationStatus.pending,
        fail: true,
      );
      await _pump(tester, controller);

      expect(find.text('Could not load your account status.'), findsOneWidget);
      final retry = find.text('Retry');
      expect(retry, findsOneWidget);

      await tester.tap(retry);
      await tester.pumpAndSettle();
      expect(controller.refreshCalls, greaterThanOrEqualTo(2));
    });

    testWidgets('sign-out clears the session and routes to sign-in',
        (tester) async {
      final controller =
          _FakeAuthController(status: RegistrationStatus.pending);
      await _pump(tester, controller);

      final signOut = find.widgetWithText(TextButton, 'Sign out');
      await tester.ensureVisible(signOut);
      await tester.pumpAndSettle();
      await tester.tap(signOut);
      await tester.pumpAndSettle();

      expect(controller.signedOut, isTrue);
      expect(find.text('SIGN-IN'), findsOneWidget);
    });
  });
}
