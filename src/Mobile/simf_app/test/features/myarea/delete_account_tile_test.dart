import 'package:flutter/material.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/myarea/widgets/delete_account_tile.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

/// Records whether the erase actually reached the repository. `implements`
/// rather than a subclass because the real constructor needs a SimfApiClient.
class _FakeProfileRepository implements ProfileRepository {
  int deleteCalls = 0;
  bool throwOnDelete = false;

  @override
  Future<void> deleteMyAccount() async {
    deleteCalls++;
    if (throwOnDelete) {
      throw const ApiFailure(
        code: 'SERVER_ERROR',
        message: 'boom',
        httpStatus: 500,
      );
    }
  }

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

    testWidgets('calls the erase once the user confirms', (tester) async {
      final repository = _FakeProfileRepository();
      final auth = _FakeAuth();
      await _pumpTile(tester, repository, auth: auth);

      await tester.tap(find.text('Delete my account'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Delete for ever'));
      await tester.pumpAndSettle();

      expect(repository.deleteCalls, 1);
      // Signed out AND moved off the profile - a dead session left on screen
      // would show a signed-in shell for an account that no longer exists.
      expect(auth.signOutCalls, 1);
      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets('surfaces a failure instead of pretending it worked',
        (tester) async {
      // A silent failure here is the worst outcome: the user believes their
      // identity document is gone when it is not.
      final repository = _FakeProfileRepository()..throwOnDelete = true;
      await _pumpTile(tester, repository);

      await tester.tap(find.text('Delete my account'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Delete for ever'));
      await tester.pumpAndSettle();

      expect(repository.deleteCalls, 1);
      expect(find.byType(SnackBar), findsOneWidget);
    });
  });
}
