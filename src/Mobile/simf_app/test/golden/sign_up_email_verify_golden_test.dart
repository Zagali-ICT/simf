@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/sign_up_email_verify_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the sign-up email-verification screen against Figma frame
/// **505:837** (التحقق بالبريد · Page 006). Regenerate: flutter test
/// --update-goldens test/golden/sign_up_email_verify_golden_test.dart
///
/// Frame parity: the navy scaffold + sweep, the back/title header, the gold
/// mail mark, the أدخل رمز التحقق title, the "sent a code to `<email>`" lines,
/// the six OTP boxes, the gold تحقق CTA and the resend row. Captured in the
/// initial state — the resend cooldown only starts after a resend, so no
/// countdown shows (resend is immediately available) and there is no active
/// timer (settle is safe). Locks the layout, typography, colour, spacing and
/// RTL of the clean-code-frozen page (D-553).
void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Sign-up email verify @375x812 — Figma 505:837 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/verify',
      routes: <RouteBase>[
        GoRoute(
          path: '/verify',
          builder: (_, __) =>
              const SignUpEmailVerifyScreen(email: 'r.alsalem@xxx.sa'),
        ),
        GoRoute(
          path: '/sign-in',
          name: RouteNames.signIn,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/sign-up',
          name: RouteNames.signUpForm,
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
      find.byType(SignUpEmailVerifyScreen),
      matchesGoldenFile('goldens/sign_up_email_verify_505-837.png'),
    );
  });
}
