@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/guest/guest_mode_screen.dart';

import 'golden_fonts.dart';

/// Render-lock golden of the Guest-mode entry screen (وضع الضيف, Page 012,
/// D-316). Regenerate:
///   flutter test --update-goldens test/golden/guest_mode_golden_test.dart
///
/// No KSA design frame is bound (it is styled to the Page 012 mockup, not a
/// pinned node), so this is a **structural** render-lock: the navy AppBar shell,
/// the accent explore ring, the headline, the accent "browsing as a guest"
/// callout, and the Continue-as-guest / Sign-in actions. RTL.

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Guest mode @375x812 — entry screen (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/guest',
      routes: <RouteBase>[
        GoRoute(
          name: RouteNames.guestMode,
          path: '/guest',
          builder: (_, __) => const GuestModeScreen(),
        ),
        for (final (name, path) in <(String, String)>[
          (RouteNames.home, '/'),
          (RouteNames.signIn, '/sign-in'),
        ])
          GoRoute(
            name: name,
            path: path,
            builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
          ),
      ],
    );

    await tester.pumpWidget(
      MaterialApp.router(
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
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(GuestModeScreen),
      matchesGoldenFile('goldens/guest_mode.png'),
    );
  });
}
