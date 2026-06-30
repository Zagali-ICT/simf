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
import 'package:simf_app/features/account/badge_sign_in_screen.dart';

import 'golden_fonts.dart';

/// Render-regression lock for the badge-QR sign-in entry. **Unbound** auth
/// screen — a thin wrapper over the shared `QrScanView` (manual-first, camera
/// off in tests); no dedicated Figma node (D-558). Regenerate:
///   flutter test --update-goldens test/golden/badge_sign_in_golden_test.dart
void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Badge sign-in @375x812 — QrScanView (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/badge-sign-in',
      routes: <RouteBase>[
        GoRoute(
          path: '/badge-sign-in',
          builder: (_, __) => const BadgeSignInScreen(enableCamera: false),
        ),
        GoRoute(
          path: '/sign-in',
          name: RouteNames.signIn,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/badge-activation',
          name: RouteNames.badgeActivation,
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
      find.byType(BadgeSignInScreen),
      matchesGoldenFile('goldens/badge_sign_in.png'),
    );
  });
}
