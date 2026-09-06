import 'package:flutter/material.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/delete_account_code_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

class _FakeProfileRepository implements ProfileRepository {
  int sendCalls = 0;
  ApiFailure? sendFailure;

  @override
  Future<AccountDeletionCode> sendDeletionCode() async {
    sendCalls++;
    if (sendFailure != null) {
      throw sendFailure!;
    }
    return const AccountDeletionCode(
      maskedEmail: 'd***@simf.test',
      expiresInSeconds: 600,
    );
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

Future<void> _pump(
  WidgetTester tester,
  _FakeProfileRepository repository, {
  Future<void> Function(String code)? erase,
}) async {
  // A real GoRouter: the success path pops a result, and that pop IS the
  // contract with the tile.
  final router = GoRouter(
    initialLocation: '/from',
    routes: <RouteBase>[
      GoRoute(
        path: '/from',
        builder: (context, _) => Scaffold(
          body: Center(
            child: TextButton(
              onPressed: () => context.push('/code'),
              child: const Text('OPEN'),
            ),
          ),
        ),
      ),
      GoRoute(
        path: '/code',
        builder: (_, __) => DeleteAccountCodeScreen(erase: erase),
      ),
    ],
  );

  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        profileRepositoryProvider.overrideWithValue(repository),
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
  await tester.tap(find.text('OPEN'));
  await tester.pumpAndSettle();
}

Future<void> _enterCode(WidgetTester tester, String code) async {
  await tester.enterText(find.byType(TextField).first, code);
  await tester.pumpAndSettle();
}

void main() {
  group('DeleteAccountCodeScreen', () {
    testWidgets('asks for a code on open and names the masked recipient',
        (tester) async {
      final repository = _FakeProfileRepository();
      await _pump(tester, repository, erase: (_) async {});

      expect(repository.sendCalls, 1);
      expect(find.textContaining('d***@simf.test'), findsOneWidget);
    });

    testWidgets('submits the entered code to the erase it was handed',
        (tester) async {
      final repository = _FakeProfileRepository();
      String? submitted;
      await _pump(
        tester,
        repository,
        erase: (code) async => submitted = code,
      );

      await _enterCode(tester, '123456');
      await tester.tap(find.text('Delete for ever'));
      await tester.pumpAndSettle();

      expect(submitted, '123456');
    });

    testWidgets('a wrong code keeps the user here and sends NO new code',
        (tester) async {
      // The reason this screen submits rather than popping a verified code
      // back to the tile. Re-entering would send a fresh code every time, and
      // five an hour is the cap - four typos would lock deletion out for an
      // hour.
      final repository = _FakeProfileRepository();
      await _pump(
        tester,
        repository,
        erase: (_) async => throw const ApiFailure(
          code: 'ACCOUNT_DELETION_CODE_INVALID',
          message: 'server text nobody should see',
          httpStatus: 403,
        ),
      );

      await _enterCode(tester, '111111');
      await tester.tap(find.text('Delete for ever'));
      await tester.pumpAndSettle();

      expect(find.byType(DeleteAccountCodeScreen), findsOneWidget);
      expect(repository.sendCalls, 1, reason: 'a retry must not re-send');
      // App-controlled copy, not the server envelope: the owner asked for a
      // message that says what to do next.
      expect(
        find.textContaining('not correct'),
        findsOneWidget,
      );
    });

    testWidgets('an expired code offers a new one immediately', (tester) async {
      final repository = _FakeProfileRepository();
      await _pump(
        tester,
        repository,
        erase: (_) async => throw const ApiFailure(
          code: 'ACCOUNT_DELETION_CODE_EXPIRED',
          message: '',
          httpStatus: 403,
        ),
      );

      await _enterCode(tester, '123456');
      await tester.tap(find.text('Delete for ever'));
      await tester.pumpAndSettle();

      expect(find.textContaining('expired'), findsOneWidget);
      // The countdown is zeroed rather than left running, so "request a new
      // one" is not advice the screen refuses to act on.
      await tester.tap(find.textContaining("Didn't get the code?"));
      await tester.pumpAndSettle();
      expect(repository.sendCalls, 2);
    });

    testWidgets('a rate-limited send is reported in the app own copy',
        (tester) async {
      final repository = _FakeProfileRepository()
        ..sendFailure = const ApiFailure(
          code: 'RATE_LIMIT_EXCEEDED',
          message: '',
          httpStatus: 429,
        );
      await _pump(tester, repository, erase: (_) async {});

      expect(find.textContaining('Too many codes'), findsOneWidget);
    });

    testWidgets('with no erase handed over it refuses and sends nothing',
        (tester) async {
      // Android process death drops the route's extra. Guessing at a
      // repository here would make the hand-over decorative.
      final repository = _FakeProfileRepository();
      await _pump(tester, repository);

      expect(repository.sendCalls, 0);
      expect(
        find.textContaining('Could not start account deletion'),
        findsOneWidget,
      );
    });
  });
}
