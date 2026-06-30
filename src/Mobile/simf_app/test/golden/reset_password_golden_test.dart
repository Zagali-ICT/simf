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
import 'package:simf_app/features/account/reset_password_screen.dart';

import 'golden_fonts.dart';

/// Render-regression lock for the reset-password screen. **Unbound** auth screen
/// on the shared KSA auth chrome (`SimfFormScaffold`); no dedicated Figma node
/// (D-557). Regenerate:
///   flutter test --update-goldens test/golden/reset_password_golden_test.dart
void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Reset password @375x812 — KSA auth chrome (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/reset',
      routes: <RouteBase>[
        GoRoute(
          path: '/reset',
          builder: (_, __) =>
              const ResetPasswordScreen(email: 'r.alsalem@xxx.sa'),
        ),
        GoRoute(
          path: '/sign-in',
          name: RouteNames.signIn,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
        GoRoute(
          path: '/forgot',
          name: RouteNames.forgotPassword,
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
      find.byType(ResetPasswordScreen),
      matchesGoldenFile('goldens/reset_password.png'),
    );
  });
}
