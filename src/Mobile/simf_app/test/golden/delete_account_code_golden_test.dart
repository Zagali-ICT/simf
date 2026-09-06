@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/data/profile_models.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/account/delete_account_code_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the account-deletion confirmation screen. Like the
/// biometric step-up it reuses the shared KSA OTP frame with no dedicated Figma
/// node, so this locks a render regression rather than proving parity (D-554).
/// Regenerate:
///   flutter test --update-goldens test/golden/delete_account_code_golden_test.dart
///
/// Captured after the on-open code request resolves, so the masked recipient is
/// present and the countdown sits at its 01:00 start.
class _FakeProfileRepository implements ProfileRepository {
  @override
  Future<AccountDeletionCode> sendDeletionCode() async =>
      const AccountDeletionCode(
        maskedEmail: 'r***@xxx.sa',
        expiresInSeconds: 600,
      );

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Delete-account code @375x812 — KSA OTP frame (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/delete-code',
      routes: <RouteBase>[
        GoRoute(
          path: '/delete-code',
          builder: (_, __) => DeleteAccountCodeScreen(erase: (_) async {}),
        ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          profileRepositoryProvider.overrideWithValue(_FakeProfileRepository()),
        ],
        child: MaterialApp.router(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          routerConfig: router,
          locale: const Locale('ar'),
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
    // initState fires the send; pump frames (under 1s) to resolve it and the
    // asset futures while the countdown stays at its 01:00 start.
    await tester.pump();
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 200));

    await expectLater(
      find.byType(DeleteAccountCodeScreen),
      matchesGoldenFile('goldens/delete_account_code.png'),
    );
  });
}
