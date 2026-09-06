import 'package:flutter/material.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/myarea/widgets/delete_account_tile.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

/// Records whether the erase actually reached the repository. `implements`
/// rather than a subclass because the real constructor needs a SimfApiClient.
class _FakeProfileRepository implements ProfileRepository {
  int deleteCalls = 0;
  String? deletedWithCode;
  bool throwOnDelete = false;

  @override
  Future<void> deleteMyAccount(String code) async {
    deleteCalls++;
    deletedWithCode = code;
    if (throwOnDelete) {
      throw const ApiFailure(
        code: 'SERVER_ERROR',
        message: 'boom',
        httpStatus: 500,
      );
    }
  }

  @override
  Future<AccountDeletionCode> sendDeletionCode() async =>
      const AccountDeletionCode(
        maskedEmail: 'd***@simf.test',
        expiresInSeconds: 600,
      );

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}


/// The real AuthController.signOut() reaches secure storage, which no widget
/// test has. This records that the tile signed the user out without pulling
/// the plugin in.
class _FakeAuth extends AuthController {
  int signOutCalls = 0;

  @override
  AuthState build() => const AuthStateSignedOut();

  @override
  Future<void> signOut() async {
    signOutCalls++;
  }
}

Future<void> _pumpTile(
  WidgetTester tester,
  _FakeProfileRepository repository, {
  _FakeAuth? auth,
}) async {
  // A real GoRouter, because the tile lands on sign-in after a successful
  // erase - that navigation IS the behaviour, not decoration.
  final router = GoRouter(
    initialLocation: '/',
    routes: <RouteBase>[
      GoRoute(
        path: '/',
        builder: (_, __) => const Scaffold(body: DeleteAccountTile()),
      ),
      GoRoute(
        name: RouteNames.signIn,
        path: '/sign-in',
        builder: (_, __) => const Scaffold(body: Text('SIGN-IN')),
      ),
      // Stands in for DeleteAccountCodeScreen. The contract under test is that
      // the tile hands over its own erase call and acts on the result, so this
      // does exactly what the real screen does: run it, pop true.
      GoRoute(
        name: RouteNames.deleteAccountCode,
        path: '/account/delete-code',
        builder: (context, state) {
          final erase = state.extra! as Future<void> Function(String);
          return Scaffold(
            body: Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  const Text('CODE-SCREEN'),
                  TextButton(
                    onPressed: () async {
                      // Mirrors the real screen: a refusal keeps the user HERE
                      // with the reason, and never pops success.
                      try {
                        await erase('123456');
                      } on ApiFailure catch (_) {
                        return;
                      }
                      if (context.mounted) {
                        context.pop(true);
                      }
                    },
                    child: const Text('SUBMIT-CODE'),
                  ),
                  TextButton(
                    onPressed: () => context.pop(),
                    child: const Text('ABANDON'),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    ],
  );

  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        profileRepositoryProvider.overrideWithValue(repository),
        if (auth != null) authControllerProvider.overrideWith(() => auth),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: const Locale('en'),
        localizationsDelegates: AppL10n.localizationsDelegates,
        supportedLocales: AppL10n.supportedLocales,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('DeleteAccountTile', () {
    testWidgets('does NOT delete when the confirmation is cancelled',
        (tester) async {
      // The whole point of the dialog. A destructive, irreversible action that
      // fired on the first tap would be the defect.
      final repository = _FakeProfileRepository();
      await _pumpTile(tester, repository);

      await tester.tap(find.text('Delete my account'));
      await tester.pumpAndSettle();
      expect(find.text('Delete account permanently'), findsOneWidget);

      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();

      expect(repository.deleteCalls, 0);
    });

    testWidgets('confirming opens the code screen, and does not erase yet',
        (tester) async {
      // Erasure now needs an emailed code, so confirming the dialog must not
      // be the point of no return.
      final repository = _FakeProfileRepository();
      await _pumpTile(tester, repository);

      await tester.tap(find.text('Delete my account'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Delete for ever'));
      await tester.pumpAndSettle();

      expect(find.text('CODE-SCREEN'), findsOneWidget);
      expect(repository.deleteCalls, 0);
    });

    testWidgets('erases with the code, then signs out and leaves the profile',
        (tester) async {
      final repository = _FakeProfileRepository();
      final auth = _FakeAuth();
      await _pumpTile(tester, repository, auth: auth);

      await tester.tap(find.text('Delete my account'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Delete for ever'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('SUBMIT-CODE'));
      await tester.pumpAndSettle();

      expect(repository.deleteCalls, 1);
      expect(repository.deletedWithCode, '123456');
      // Signed out AND moved off the profile - a dead session left on screen
      // would show a signed-in shell for an account that no longer exists.
      expect(auth.signOutCalls, 1);
      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets('abandoning the code screen erases nothing and stays put',
        (tester) async {
      final repository = _FakeProfileRepository();
      final auth = _FakeAuth();
      await _pumpTile(tester, repository, auth: auth);

      await tester.tap(find.text('Delete my account'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Delete for ever'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('ABANDON'));
      await tester.pumpAndSettle();

      expect(repository.deleteCalls, 0);
      expect(auth.signOutCalls, 0);
      expect(find.text('Delete my account'), findsOneWidget);
    });

    testWidgets('a refused erase never signs the user out', (tester) async {
      // A silent failure here is the worst outcome: the user believes their
      // identity document is gone when it is not. The code screen owns showing
      // the reason; what the TILE must not do is proceed as if it worked.
      final repository = _FakeProfileRepository()..throwOnDelete = true;
      final auth = _FakeAuth();
      await _pumpTile(tester, repository, auth: auth);

      await tester.tap(find.text('Delete my account'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Delete for ever'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('SUBMIT-CODE'));
      await tester.pumpAndSettle();

      expect(repository.deleteCalls, 1);
      expect(auth.signOutCalls, 0);
      expect(find.text('SIGN-IN'), findsNothing);
    });
  });
}
