@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/account/email_otp_verify_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the email-OTP second-factor screen against Figma frame
/// **758:2616** (التحقق بالبريد · Page 003 2FA). Regenerate:
///   flutter test --update-goldens test/golden/email_otp_verify_golden_test.dart
///
/// Frame parity: the navy scaffold + sweep, the back/title header, the gold
/// mail mark, the أدخل رمز التحقق title, the "sent a code to <email>" lines, the
/// six OTP boxes, the resend countdown, the gold تحقق CTA, and the resend row.
/// Captured in the empty/default state (countdown at its 01:00 start — the
/// periodic timer means we pump frames rather than settle). Locks the layout,
/// typography, colour, spacing and RTL of the clean-code-frozen page (D-552).
class _AwaitingOtpController extends AuthController {
  @override
  AuthState build() =>
      const AuthStateAwaitingOtp('otp-token', email: 'r.alsalem@xxx.sa');
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Email-OTP @375x812 — Figma 758:2616 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/auth/verify-otp',
      routes: <RouteBase>[
        GoRoute(
          path: '/auth/verify-otp',
          name: RouteNames.verifyOtp,
          builder: (_, __) => const EmailOtpVerifyScreen(),
        ),
        GoRoute(
          path: '/sign-in',
          name: RouteNames.signIn,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          authControllerProvider.overrideWith(_AwaitingOtpController.new),
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
    // The screen runs a 1s periodic resend timer, so pumpAndSettle would never
    // settle — pump a couple of frames (under 1s) to resolve the SVG/asset
    // futures while the countdown stays at its 01:00 start.
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));

    await expectLater(
      find.byType(EmailOtpVerifyScreen),
      matchesGoldenFile('goldens/email_otp_758-2616.png'),
    );
  });
}
