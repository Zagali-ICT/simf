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
import 'package:simf_app/core/site_settings/site_settings.dart';
import 'package:simf_app/features/registration/registration_success_screen.dart';

import 'golden_fonts.dart';

/// Golden render of the Registration-success screen against Figma frame
/// **505:1451** (تم التسجيل). Regenerate: flutter test --update-goldens
/// test/golden/registration_success_golden_test.dart
///
/// Frame parity expected: the navy-surface page with the decorative top-right
/// diagonal sweep, the back + centred title header, a 104px green ring around
/// the check, the white "تم التسجيل بنجاح" headline over the beige welcome
/// copy, the navy-80% reference-number card (beige label over the gold LTR
/// `SIMF-2026-xxxx` mask), the full-width gold حالة التسجيل button + the
/// outlined الانتقال للرئيسية button, and the تواصل معنا block (title, the call
/// + mail tiles, the social footer). RTL throughout.
///
/// Fixed site-settings (no real fetch) so the PNG is stable run-to-run; no
/// [RegistrationSuccessScreen.referenceNumber] is passed so the deterministic
/// mask renders.

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Registration-success @375x900 — Figma 505:1451 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/registration/success',
      routes: <RouteBase>[
        GoRoute(
          name: RouteNames.registrationSuccess,
          path: '/registration/success',
          builder: (_, __) => const RegistrationSuccessScreen(),
        ),
        GoRoute(
          name: RouteNames.registrationStatus,
          path: '/registration/status',
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          name: RouteNames.home,
          path: '/',
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          siteSettingsProvider.overrideWith(
            (ref) => const SiteSettings(
              registrationMessageAr:
                  'تهانينا، مرحباً بكم في الملتقى السعودي الرابع',
              registrationMessageEn: 'Welcome to the Fourth Saudi Forum!',
              social: SiteSocialLinks(),
            ),
          ),
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
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(RegistrationSuccessScreen),
      matchesGoldenFile('goldens/registration_success_505-1451.png'),
    );
  });
}
