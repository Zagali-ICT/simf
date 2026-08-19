@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/forgot_password_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Parity lock for the forgot-password screen against Figma node **918:2341**
/// (نسيت كلمة المرور — owner-bound 2026-07-06, D-656): the navy surface, the
/// back + centred-title header, the gold-ringed lock mark, the beige-bordered
/// email field with the mail glyph, the gold CTA and the "remembered? sign in"
/// foot. Regenerate:
///   flutter test --update-goldens test/golden/forgot_password_golden_test.dart
void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Forgot password @375x812 — KSA auth chrome (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/forgot',
      routes: <RouteBase>[
        GoRoute(
          path: '/forgot',
          builder: (_, __) => const ForgotPasswordScreen(),
        ),
        GoRoute(
          path: '/sign-in',
          name: RouteNames.signIn,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/reset',
          name: RouteNames.resetPassword,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
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
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(ForgotPasswordScreen),
      matchesGoldenFile('goldens/forgot_password.png'),
    );
  });
}
