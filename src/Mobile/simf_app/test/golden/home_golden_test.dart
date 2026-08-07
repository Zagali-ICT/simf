@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/home/widgets/guest_home.dart';
import 'package:simf_app/features/home/widgets/visitor_home.dart';
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';

import 'golden_fonts.dart';

/// Golden render of the signed-in Home (`VisitorHome`, Figma frame **758:1134**)
/// and the guest Home (`GuestHome`, **758:2910**). Compare to the frames:
///   flutter test --update-goldens test/golden/home_golden_test.dart
///
/// The greeting is a static "مرحبًا" + first name (owner 2026-07-21), so no
/// clock is injected. The highlights carousel's auto-advance timer means the
/// render must `pump`, never `pumpAndSettle`.

// The news-image URL base (VisitorHome takes it as a plain param — the network
// images fall back in tests, no HTTP).
const _baseUrl = 'http://test.local/api/v1';

const _allSocial = OrgSocial(
  x: 'https://x.com/simf',
  instagram: 'https://instagram.com/simf',
  linkedin: 'https://linkedin.com/company/simf',
  youtube: 'https://youtube.com/@simf',
  tiktok: 'https://tiktok.com/@simf',
);

OrgProfile _orgProfile() => const OrgProfile(
      name: '',
      nameArabic: '',
      title: '',
      titleArabic: '',
      currentYear: 0,
      status: '',
      social: _allSocial,
      aboutItems: <OrgAboutItem>[],
      details: <OrgDetail>[],
    );

class _FakeOrgProfile extends OrgProfileController {
  @override
  OrgProfile? build() => _orgProfile();
}

NewsListItem _post(String id, String titleAr) => NewsListItem(
      id: id,
      title: titleAr,
      titleArabic: titleAr,
      category: 'News',
      categoryArabic: 'أخبار',
      publishedAt: DateTime.utc(2026, 1, 1, 9),
      excerpt: '',
      excerptArabic: '',
    );

final _highlights = <NewsListItem>[
  _post('n1', 'انطلاق فعاليات الملتقى البحري الدولي 2026'),
  _post('n2', 'جلسات حوارية حول مستقبل الأمن البحري'),
];

Widget _wrap(Widget home) => ProviderScope(
      overrides: <Override>[
        // A fixed unread count so the bell badge renders deterministically.
        unreadNotificationCountProvider.overrideWith((ref) async => 3),
        orgProfileProvider.overrideWith(_FakeOrgProfile.new),
      ],
      child: MaterialApp(
        debugShowCheckedModeBanner: false,
        theme: SimfTheme.dark(),
        locale: const Locale('ar'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        // A minimal router so the shell's nav / pushNamed targets resolve.
        home: home,
      ),
    );

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Home — signed-in @375x1750 — Figma 758:1134 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 1750);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      _wrap(
        Builder(
          builder: (context) => VisitorHome(
            l10n: AppL10n.of(context),
            name: 'أحمد محمد',
            baseUrl: _baseUrl,
            onRefresh: () async {},
            highlights: _highlights,
            // Meeting-eligible so the "اللقاءات الثنائية" tile renders (matches the
            // Figma home 758:1134, which shows it) — D-745.
            canRequestMeetings: true,
          ),
        ),
      ),
    );
    // pump (NOT pumpAndSettle) — the carousel auto-advance timer never settles.
    await tester.pump(const Duration(milliseconds: 100));

    await expectLater(
      find.byType(VisitorHome),
      matchesGoldenFile('goldens/home_signed_in_758-1134.png'),
    );
  });

  testWidgets('Home — guest @375x812 — Figma 758:2910 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/',
      routes: <RouteBase>[
        GoRoute(path: '/', builder: (context, __) => const _GuestHomeHost()),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          unreadNotificationCountProvider.overrideWith((ref) async => 0),
          orgProfileProvider.overrideWith(_FakeOrgProfile.new),
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
      find.byType(GuestHome),
      matchesGoldenFile('goldens/home_guest_758-2910.png'),
    );
  });
}

/// Hosts the guest home under a real route so its back-affordance / pushNamed
/// resolve (the guest home is otherwise route-agnostic).
class _GuestHomeHost extends ConsumerWidget {
  const _GuestHomeHost();

  @override
  Widget build(BuildContext context, WidgetRef ref) =>
      GuestHome(l10n: AppL10n.of(context), pendingApproval: false);
}
