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
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/about/about_screen.dart';
import 'package:simf_app/features/content/data/content_models.dart';
import 'package:simf_app/features/content/data/content_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the About screen against Figma frame **1116:16448** (عن
/// الملتقى). Regenerate:
///   flutter test --update-goldens test/golden/about_golden_test.dart
///
/// Frame parity expected: the anchor-mark header (forum name), then the الرسالة
/// + الرؤية text cards, the تفاصيل الملتقى details card (year / date / location)
/// and the المحاور الرئيسية themes card (four numbered themes). RTL. Rendered on
/// the static fallback path (no org profile, content fetch fails) so the PNG is
/// deterministic.

/// A content repo that always fails → the screen keeps its static vision copy.
class _FailContentRepo implements ContentRepository {
  @override
  Future<ContentBlock> getContentBlock(String key) async {
    throw const ApiFailure(
      code: ApiErrorCodes.clientNetwork,
      message: 'x',
      httpStatus: 404,
    );
  }
}

class _NullOrgProfile extends OrgProfileController {
  @override
  OrgProfile? build() => null;
  @override
  Future<void> warm() async {}
}

const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('About @375x1400 — Figma 1116:16448 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 1400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/about',
      routes: <RouteBase>[
        GoRoute(
          path: '/about',
          name: RouteNames.aboutForum,
          builder: (_, __) => const AboutScreen(),
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
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_testConfig),
          contentRepositoryProvider.overrideWithValue(_FailContentRepo()),
          orgProfileProvider.overrideWith(_NullOrgProfile.new),
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
      find.byType(AboutScreen),
      matchesGoldenFile('goldens/about_1116-16448.png'),
    );
  });
}
