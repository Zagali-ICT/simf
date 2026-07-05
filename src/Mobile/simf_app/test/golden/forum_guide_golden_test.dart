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
import 'package:simf_app/features/forum_guide/forum_guide_screen.dart';

import 'golden_fonts.dart';

/// Golden render of the Forum-guide screen against Figma frame **1388:7493**
/// (دليل الملتقى). Regenerate:
///   flutter test --update-goldens test/golden/forum_guide_golden_test.dart
///
/// Frame parity expected: the gold intro banner (welcome copy + guide glyph),
/// then five numbered step cards — a gold index badge, the title over the muted
/// description, and a decorative gold caret — on the navy-deep card chrome. RTL.

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Forum guide @375x900 — Figma 1388:7493 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/forum-guide',
      routes: <RouteBase>[
        GoRoute(
          path: '/forum-guide',
          name: RouteNames.forumGuide,
          builder: (_, __) => const ForumGuideScreen(),
        ),
        for (final (name, path) in <(String, String)>[
          (RouteNames.home, '/'),
          (RouteNames.sessions, '/sessions'),
          (RouteNames.badge, '/badge'),
          (RouteNames.venueMap, '/map'),
          (RouteNames.myArea, '/my-area'),
        ])
          GoRoute(
            name: name,
            path: path,
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
      find.byType(ForumGuideScreen),
      matchesGoldenFile('goldens/forum_guide_1388-7493.png'),
    );
  });
}
