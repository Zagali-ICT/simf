import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/media_partners/media_partners_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

const _partners = <MediaPartner>[
  MediaPartner(id: 'p1', name: 'Al Arabiya', nameArabic: 'العربية'),
  MediaPartner(id: 'p2', name: 'SPA', nameArabic: 'واس'),
];

// The screen reads the base URL to build the logo asset URL; every pump
// supplies one (the must-override config provider throws otherwise). The test
// HTTP client fails network-image loads, so the logo's errorBuilder falls back
// to the initials tile — no real bytes are needed.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

Future<void> _pump(
  WidgetTester tester, {
  required List<MediaPartner> Function() partners,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/media-partners',
    routes: <RouteBase>[
      GoRoute(
        path: '/media-partners',
        name: RouteNames.mediaPartners,
        builder: (_, __) => const MediaPartnersScreen(),
      ),
      // The shell back-target + the two sibling coverage tabs the tab bar
      // navigates to. Plus the bottom-nav destinations SimfPageShell renders.
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.home, '/', 'HOME'),
        (RouteNames.news, '/news', 'NEWS'),
        (RouteNames.gallery, '/media', 'GALLERY'),
        (RouteNames.badge, '/badge', 'BADGE'),
        (RouteNames.venueMap, '/map', 'MAP'),
        (RouteNames.myArea, '/my-area', 'MY-AREA'),
        (RouteNames.sessions, '/sessions', 'SESSIONS'),
        (RouteNames.notifications, '/notifications', 'NOTIFS'),
      ])
        GoRoute(
          name: name,
          path: path,
          builder: (c, s) => Scaffold(body: Text(label)),
        ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_testConfig),
        mediaPartnersProvider.overrideWith((ref) async => partners()),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: locale,
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
}

void main() {
  group('MediaPartnersScreen (Page 031 — KSA frame 947:3764)', () {
    testWidgets('renders the media-center header, the two tabs and the partners',
        (tester) async {
      await _pump(tester, partners: () => _partners);

      // The media-center header (the container, not the bare tab label).
      expect(find.text('Media center'), findsWidgets);
      // The two media-center tabs (the Gallery tab was dropped — Figma 947/1049).
      expect(find.text('Latest updates'), findsOneWidget);
      expect(find.text('Media partners'), findsWidgets);
      expect(find.text('Media gallery'), findsNothing);
      // A card per partner, captioned with its name.
      expect(find.text('Al Arabiya'), findsOneWidget);
      expect(find.text('SPA'), findsOneWidget);
    });

    testWidgets('builds each logo from the public asset route', (tester) async {
      await _pump(tester, partners: () => _partners);

      final urls = tester
          .widgetList<Image>(find.byType(Image))
          .map((image) => image.image)
          .whereType<NetworkImage>()
          .map((provider) => provider.url)
          .toList();
      // Each card builds its logo URL via MediaPartner.logoAssetUrl (the single
      // source of the D-357 route shape).
      for (final partner in _partners) {
        expect(urls, contains(partner.logoAssetUrl(_testConfig.baseUrl)));
      }
    });

    testWidgets('a partner with no logo falls back to its initials on the tile',
        (tester) async {
      // The test HTTP client fails the network-image load, so each logo's
      // errorBuilder renders the initials tile — "Al Arabiya" → AA, "SPA" → S.
      await _pump(tester, partners: () => _partners);
      expect(find.text('AA'), findsOneWidget);
      expect(find.text('S'), findsOneWidget);
    });

    testWidgets('tapping the active Media-partners tab does not navigate away',
        (tester) async {
      await _pump(tester, partners: () => _partners);

      await tester.tap(find.text('Media partners'));
      await tester.pumpAndSettle();
      // Still on the same screen — the active pill has no onTap.
      expect(find.byType(MediaPartnersScreen), findsOneWidget);
    });

    testWidgets('tapping the Latest-updates tab navigates to the news route',
        (tester) async {
      await _pump(tester, partners: () => _partners);

      await tester.tap(find.text('Latest updates'));
      await tester.pumpAndSettle();
      expect(find.text('NEWS'), findsOneWidget);
    });

    testWidgets('empty shows the empty state', (tester) async {
      await _pump(tester, partners: () => const <MediaPartner>[]);
      expect(find.text('No media partners'), findsOneWidget);
    });

    testWidgets('a read failure shows the error + retry', (tester) async {
      await _pump(tester, partners: () => throw Exception('x'));
      expect(find.text('Could not load the media partners.'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Retry'), findsOneWidget);
    });

    // D-436 verification rule: confirm RTL placement with a deterministic
    // Arabic-locale position test, not a visual claim. In RTL a Row lays its
    // children inline-start→end = right→left, so the first tab (partners) sits
    // right-most and the second (latest-updates) left — matching frame 947:3764.
    testWidgets('lays the tabs partners→latest right-to-left in Arabic',
        (tester) async {
      await _pump(tester, partners: () => _partners, locale: const Locale('ar'));

      final l10n = AppL10n.of(tester.element(find.byType(MediaPartnersScreen)));

      // The media-center header renders.
      expect(find.text('المركز الاعلامي'), findsWidgets);
      // The whole screen is RTL.
      expect(
        Directionality.of(tester.element(find.text(l10n.mediaPartnersTitle))),
        TextDirection.rtl,
      );

      final partnersDx = tester.getCenter(find.text(l10n.mediaPartnersTitle)).dx;
      final latestDx = tester.getCenter(find.text(l10n.latestUpdatesTitle)).dx;

      // partners right-most → latest-updates left.
      expect(partnersDx, greaterThan(latestDx));
    });

    test('MediaPartner.fromJson reads the wire fields', () {
      final partner = MediaPartner.fromJson(<String, dynamic>{
        'id': 'p9',
        'name': 'Reuters',
        'nameArabic': 'رويترز',
        'logoRelativePath': 'media-partners/reuters.png',
      });
      expect(partner.id, 'p9');
      expect(partner.localizedName(false), 'Reuters');
      expect(partner.localizedName(true), 'رويترز');
      expect(partner.logoRelativePath, 'media-partners/reuters.png');
    });

    test('logoAssetUrl builds the D-357 anonymous asset route', () {
      const partner = MediaPartner(id: 'p9', name: 'X', nameArabic: 'س');
      expect(
        partner.logoAssetUrl('http://h/api/v1'),
        'http://h/api/v1/app/assets/MediaPartnerLogo/p9/image',
      );
    });
  });
}
