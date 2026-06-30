import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/forgot_password_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// A fake repository that records the [forgotPassword] call so the test can
/// prove client-side validation gates (or proceeds past) the request. Every
/// other [AuthRepository] member is unused on this screen and throws via
/// [noSuchMethod] if touched.
class _FakeAuthRepository implements AuthRepository {
  String? requestedEmail;

  @override
  Future<void> forgotPassword({required String email}) async {
    requestedEmail = email;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError(invocation.memberName.toString());
}

Future<void> _pump(WidgetTester tester, _FakeAuthRepository repo) async {
  final router = GoRouter(
    initialLocation: '/auth/forgot-password',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.forgotPassword,
        path: '/auth/forgot-password',
        builder: (c, s) => const ForgotPasswordScreen(),
      ),
      GoRoute(
        name: RouteNames.resetPassword,
        path: '/auth/reset-password',
        builder: (c, s) => const Scaffold(body: Text('RESET')),
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
        authRepositoryProvider.overrideWithValue(repo),
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
  group('ForgotPasswordScreen (Page 003 — email validation)', () {
    testWidgets('an empty email blocks submit and shows the required error',
        (tester) async {
      final repo = _FakeAuthRepository();
      await _pump(tester, repo);

      // The button is disabled while the field is empty, so drive the field's
      // own validation by submitting from the keyboard with whitespace only.
      await tester.enterText(find.byType(TextFormField), '   ');
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pumpAndSettle();

      expect(find.text('This field is required'), findsOneWidget);
      expect(find.text('RESET'), findsNothing);
      expect(repo.requestedEmail, isNull);
    });

    testWidgets('a malformed email shows the inline error and does not request '
        'a reset', (tester) async {
      final repo = _FakeAuthRepository();
      await _pump(tester, repo);

      await tester.enterText(find.byType(TextFormField), 'not-an-email');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Send code'));
      await tester.pumpAndSettle();

      expect(find.text('Invalid email'), findsOneWidget);
      expect(find.text('RESET'), findsNothing);
      expect(repo.requestedEmail, isNull);
    });

    testWidgets('a valid email passes validation and routes to the reset '
        'screen', (tester) async {
      final repo = _FakeAuthRepository();
      await _pump(tester, repo);

      await tester.enterText(find.byType(TextFormField), 'visitor@example.sa');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Send code'));
      await tester.pumpAndSettle();

      expect(find.text('RESET'), findsOneWidget);
      expect(repo.requestedEmail, equals('visitor@example.sa'));
      // No validation error survived a valid submit.
      expect(find.text('Invalid email'), findsNothing);
      expect(find.text('This field is required'), findsNothing);
    });
  });
}
