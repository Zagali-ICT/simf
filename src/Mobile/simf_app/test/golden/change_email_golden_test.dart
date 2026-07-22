@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/change_email_screen.dart';
import 'package:simf_app/features/account/data/email_change_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import 'golden_fonts.dart';

/// #24 — golden render of the self-service change-email screen. This is an
/// **unbound** auth screen: it reuses the shared navy auth chrome (no dedicated
/// Figma node), so the golden is a render-regression lock (like biometric
/// step-up / reset-password). Two frames: the new-email entry phase and the code
/// phase. Regenerate:
///   flutter test --update-goldens test/golden/change_email_golden_test.dart
class _FakeSignedInController extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(
        Session(
          accessToken: 'A',
          refreshToken: 'R',
          accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
          user: CurrentUser(
            id: 'u1',
            email: 'ahmed@example.sa',
            displayName: 'Ahmed',
            appRole: AppRole.visitor,
            preferredLanguage: PreferredLanguage.fromJson('ar'),
            registrationStatus: RegistrationStatus.approved,
          ),
        ),
      );
}

class _FakeRepo implements EmailChangeRepository {
  @override
  Future<EmailChangeCodeSent> sendOtp(String newEmail) async =>
      const EmailChangeCodeSent(
        maskedNewEmail: 'n***@example.sa',
        expiresInSeconds: 600,
        resendCooldownSeconds: 120,
      );

  @override
  Future<void> confirm({
    required String newEmail,
    required String code,
    required String currentPassword,
  }) async {}
}

Future<void> _pumpScreen(WidgetTester tester) async {
  tester.view.physicalSize = const Size(375, 812);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.reset);

  final router = GoRouter(
    initialLocation: '/change-email',
    routes: <RouteBase>[
      GoRoute(
        path: '/change-email',
        builder: (_, __) => const ChangeEmailScreen(),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        authControllerProvider.overrideWith(_FakeSignedInController.new),
        emailChangeRepositoryProvider.overrideWithValue(_FakeRepo()),
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
  await tester.pump();
  await tester.pump(const Duration(milliseconds: 200));
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Change email @375x812 — new-email phase (Arabic)',
      (tester) async {
    await _pumpScreen(tester);

    await expectLater(
      find.byType(ChangeEmailScreen),
      matchesGoldenFile('goldens/change_email_enter.png'),
    );
  });

  testWidgets('Change email @375x812 — code phase (Arabic)', (tester) async {
    await _pumpScreen(tester);

    await tester.enterText(find.byType(TextFormField), 'new@example.sa');
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'إرسال الرمز'));
    // Resolve the send + asset futures; keep under 1s so the countdown stays at
    // its 02:00 start (the 1s resend timer means we pump frames, not settle).
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 200));

    await expectLater(
      find.byType(ChangeEmailScreen),
      matchesGoldenFile('goldens/change_email_code.png'),
    );

    await tester.pumpWidget(const SizedBox()); // cancel the resend timer
  });
}
