@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/chatbot/chatbot_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the AI-assistant screen against Figma frame **1064:13066**
/// (المساعد الذكي). Regenerate: flutter test --update-goldens
/// test/golden/chatbot_golden_test.dart
///
/// Frame parity expected: the assistant's opening greeting bubble at the
/// inline-start (navy-deep + a gold "AI" badge), the horizontal quick-reply
/// chip strip, and the bottom input bar with the gold send square. RTL.
/// Greeting-only — no sending, and no scripted demo transcript (removed when
/// the screen was wired to the real `/app/ai/assistance` assistant) — so the
/// PNG is stable.

const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Chatbot @375x812 — Figma 1064:13066 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/chatbot',
      routes: <RouteBase>[
        GoRoute(
          path: '/chatbot',
          name: RouteNames.chatbot,
          builder: (_, __) => const ChatbotScreen(),
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
      simfTestScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_testConfig),
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
      find.byType(ChatbotScreen),
      matchesGoldenFile('goldens/chatbot_1064-13066.png'),
    );
  });
}
