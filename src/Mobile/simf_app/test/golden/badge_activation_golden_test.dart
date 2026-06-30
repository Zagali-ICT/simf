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
import 'package:simf_app/features/account/badge_activation_screen.dart';

import 'golden_fonts.dart';

/// Golden render of the badge-activation screen (Part B, D-430). This is an
/// **unbound** auth screen — built on the shared KSA auth chrome
/// (`SimfFormScaffold`), no dedicated Figma node — so the golden is a
/// render-regression lock rather than a parity proof (D-555). Regenerate:
///   flutter test --update-goldens test/golden/badge_activation_golden_test.dart
///
/// Captured in the email-entry step (`needsEmail: true`): the navy chrome + the
/// beige card with the title, the "enter your email" intro, the email field and
/// the gold "send code" button. (This step does not auto-send, so there is no
/// async work or timer to settle.)
void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Badge activation @375x812 — KSA auth chrome (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/badge-activation',
      routes: <RouteBase>[
        GoRoute(
          path: '/badge-activation',
          builder: (_, __) =>
              const BadgeActivationScreen(qrId: 'QR-1', needsEmail: true),
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
      find.byType(BadgeActivationScreen),
      matchesGoldenFile('goldens/badge_activation.png'),
    );
  });
}
