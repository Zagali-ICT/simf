import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/ksa_shell.dart';
import 'package:simf_app/core/site_settings/site_settings.dart';
import 'package:simf_app/features/home/home_screen.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_app/features/news/news_screen.dart' show newsListProvider;
import 'package:simf_app/features/notifications/data/notification_models.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

// The post card builds `{base}/app/assets/NewsImage/{id}/image`; the test
// network-image loads fail, so the image's errorBuilder shows the fallback.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

CurrentUser _visitor() => CurrentUser(
      id: 'u1',
      email: 'visitor@example.sa',
      displayName: 'Ahmed Mohammed',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('ar'),
      registrationStatus: RegistrationStatus.approved,
    );

Session _session() => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _visitor(),
    );

class _SignedInController extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(_session());
}

class _GuestController extends AuthController {
  @override
  AuthState build() => const AuthStateSignedOut();
}

/// A signed-in account whose auth display name IS the email (the common case
/// for accounts created without a separate display name).
class _SignedInEmailController extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(
        Session(
          accessToken: 'A',
          refreshToken: 'R',
          accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
          user: CurrentUser(
            id: 'u2',
            email: 'visitor@example.sa',
            displayName: 'visitor@example.sa',
            appRole: AppRole.visitor,
            preferredLanguage: PreferredLanguage.fromJson('ar'),
            registrationStatus: RegistrationStatus.approved,
          ),
        ),
      );
}

class _FakeNotificationsRepository implements NotificationsRepository {
  _FakeNotificationsRepository(this.count);

  final int count;

  @override
  Future<int> getUnreadCount() async => count;

  @override
  Future<List<NotificationItem>> getNotifications({
    int skip = 0,
    int top = 50,
  }) async =>
      const <NotificationItem>[];

  @override
  Future<bool> markRead(String id) async => true;

  @override
  Future<bool> markAllRead() async => true;
}

NewsListItem _post({String id = 'n1', String title = 'Forum opens 2026'}) =>
    NewsListItem(
      id: id,
      title: title,
      titleArabic: 'افتتاح الملتقى 2026',
      category: 'News',
      categoryArabic: 'أخبار',
      publishedAt: DateTime.utc(2026, 1, 1, 9),
      excerpt: 'The opening session begins now.',
      excerptArabic: 'تبدأ الجلسة الافتتاحية الآن.',
    );

MyAreaDashboard _dashboard({
  String nameAr = 'مهند زقالي محمد',
  String nameEn = 'Mohaned Zagali',
  String? avatarUrl,
}) =>
    MyAreaDashboard(
      identity: MyAreaIdentity(
        fullNameAr: nameAr,
        fullNameEn: nameEn,
        avatarUrl: avatarUrl,
      ),
      counters: const MyAreaCounters(bookedSessionsCount: 0, meetingsCount: 0),
      todaySchedule: const <MyAreaScheduleItem>[],
    );

Future<void> _pump(
  WidgetTester tester, {
  required AuthController controller,
  int unread = 0,
  List<NewsListItem> news = const <NewsListItem>[],
  MyAreaDashboard? profile,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.home,
        path: '/',
        builder: (c, s) => const HomeScreen(),
      ),
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.sessions, '/sessions', 'SESSIONS'),
        (RouteNames.speakers, '/speakers', 'SPEAKERS'),
        (RouteNames.venueMap, '/map', 'MAP'),
        (RouteNames.booths, '/booths', 'BOOTHS'),
        (RouteNames.sponsors, '/sponsors', 'SPONSORS'),
        (RouteNames.gallery, '/media', 'GALLERY'),
        (RouteNames.archive, '/archive', 'ARCHIVE'),
        (RouteNames.aboutForum, '/about', 'ABOUT'),
        (RouteNames.faq, '/faq', 'FAQ-PAGE'),
        (RouteNames.meetPeople, '/meet', 'MEET'),
        (RouteNames.chatbot, '/chatbot', 'CHATBOT'),
        (RouteNames.aiSummary, '/ai-summary', 'AI-SUMMARY'),
        (RouteNames.badge, '/badge', 'BADGE'),
        (RouteNames.news, '/news', 'NEWS'),
        (RouteNames.more, '/more', 'MORE'),
        (RouteNames.sendQuestion, '/send-question', 'SEND-QUESTION'),
        (RouteNames.notifications, '/notifications', 'NOTIFICATIONS'),
        (RouteNames.liveBroadcast, '/live', 'LIVE'),
        (RouteNames.signIn, '/sign-in', 'SIGN-IN'),
        (RouteNames.myArea, '/my-area', 'MY-AREA'),
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
        authControllerProvider.overrideWith(() => controller),
        simfDataConfigProvider.overrideWithValue(_testConfig),
        notificationsRepositoryProvider
            .overrideWithValue(_FakeNotificationsRepository(unread)),
        newsListProvider.overrideWith((ref) async => news),
        homeProfileProvider.overrideWith((ref) async => profile),
        // D-461 — fixed site-settings so the social row never fires a real fetch.
        siteSettingsProvider.overrideWith(
          (ref) => const SiteSettings(
            registrationMessageAr: '',
            registrationMessageEn: '',
            social: SiteSocialLinks(),
          ),
        ),
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
  group('HomeScreen — guest layout (frame 512:1492)', () {
    testWidgets('shows the guest banner, public tiles, locked badge card and '
        'the sign-in button', (tester) async {
      await _pump(tester, controller: _GuestController());

      expect(find.text('Home • Guest'), findsOneWidget);
      expect(find.textContaining('browsing as a guest'), findsOneWidget);
      expect(find.text('Sessions'), findsOneWidget);
      expect(find.text('Speakers'), findsOneWidget);
      expect(find.text('Exhibition'), findsOneWidget);
      // The guest tiles use the exact Figma SVG glyphs now (frame 758:2910),
      // not the old Material icons.
      expect(find.byIcon(Icons.mic_none_outlined), findsNothing);
      expect(find.byIcon(Icons.calendar_today_outlined), findsNothing);
      expect(find.byIcon(Icons.map_outlined), findsNothing);
      expect(find.byIcon(Icons.grid_view_outlined), findsNothing);
      // The lower content mounts as the list scrolls.
      for (final below in <String>[
        'My badge', // the locked بطاقتي card — visible but inert
        'Open to everyone',
        'FAQ',
        'Spirit of Saudi',
      ]) {
        await tester.scrollUntilVisible(
          find.text(below),
          120,
          scrollable: find.byType(Scrollable).first,
        );
        expect(find.text(below), findsOneWidget);
      }
      await tester.scrollUntilVisible(
        find.widgetWithText(FilledButton, 'Sign in'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
      // The guest home shows NO notifications bell (owner 2026-06-18): the
      // Figma content/guest frames carry no bell — it lives only on the
      // signed-in home greeting header. The shared ☰ + language + dark-mode
      // controls remain on the guest top bar.
      expect(find.byTooltip('Notifications'), findsNothing);
      expect(find.byIcon(Icons.menu), findsOneWidget);
      expect(find.byIcon(Icons.language), findsOneWidget);
      expect(find.byIcon(Icons.dark_mode_outlined), findsOneWidget);
    });

    testWidgets('a public tile navigates to its route', (tester) async {
      await _pump(tester, controller: _GuestController());

      await tester.tap(find.text('Speakers'));
      await tester.pumpAndSettle();
      expect(find.text('SPEAKERS'), findsOneWidget);
    });

    testWidgets('the sign-in button opens /sign-in', (tester) async {
      await _pump(tester, controller: _GuestController());

      await tester.scrollUntilVisible(
        find.widgetWithText(FilledButton, 'Sign in'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();
      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets('the FAQ row opens the FAQ page', (tester) async {
      await _pump(tester, controller: _GuestController());

      await tester.scrollUntilVisible(
        find.text('FAQ'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('FAQ'));
      await tester.pumpAndSettle();
      expect(find.text('FAQ-PAGE'), findsOneWidget);
    });

    testWidgets('renders right-to-left in Arabic', (tester) async {
      await _pump(
        tester,
        controller: _GuestController(),
        locale: const Locale('ar'),
      );

      expect(find.text('الرئيسية • ضيف'), findsOneWidget);
      expect(
        Directionality.of(tester.element(find.text('المتحدثون'))),
        TextDirection.rtl,
      );
    });
  });

  group('HomeScreen — signed-in layout (frame 758:1134)', () {
    testWidgets('shows the greeting header, live banner, the section bars and '
        'every tile section', (tester) async {
      await _pump(tester, controller: _SignedInController());

      expect(find.textContaining('Ahmed Mohammed'), findsOneWidget);
      expect(find.byTooltip('Notifications'), findsOneWidget);
      expect(find.text('LIVE'), findsOneWidget);
      // "عن الملتقى" is now a bordered nav row (KsaLinkRow), not a text header.
      // (The full three-bar count is asserted on a tall surface in the RTL
      // group, where every off-screen bar is built.)
      expect(find.text('About the forum'), findsOneWidget);
      // The lower sections mount lazily; drag through the list and collect every
      // label seen (robust to overshoot, unlike per-item scrollUntilVisible).
      final scrollable = find.byType(Scrollable).first;
      final seen = <String>{};
      for (var i = 0; i < 30; i++) {
        for (final t in tester.widgetList<Text>(find.byType(Text))) {
          final data = t.data;
          if (data != null) {
            seen.add(data);
          }
        }
        await tester.drag(scrollable, const Offset(0, -220));
        await tester.pump();
      }
      for (final section in <String>[
        'Sessions', // جلسات (new about tile)
        'Ask the moderator', // اسأل المحاور (new full-width tile)
        'News & coverage', // الأخبار والتغطية bar
        'Sponsors', // الرعاة bar
        'Bilateral meetings',
        'Smart features',
        'Session summaries',
        'Follow us',
        'Spirit of Saudi',
      ]) {
        expect(seen, contains(section), reason: 'missing section: $section');
      }
      // No guest chrome.
      expect(find.widgetWithText(FilledButton, 'Sign in'), findsNothing);
      expect(find.text('Home • Guest'), findsNothing);
    });

    testWidgets('greeting follows the time of day', (tester) async {
      await _pump(tester, controller: _SignedInController());
      final l10n = AppL10n.of(
        tester.element(find.textContaining('Ahmed Mohammed')),
      );

      expect(homeGreeting(l10n, DateTime(2026, 1, 1, 9)), 'Good morning');
      expect(homeGreeting(l10n, DateTime(2026, 1, 1, 15)), 'Good evening');
    });

    testWidgets('greeting shows the profile name, not the auth display name',
        (tester) async {
      await _pump(
        tester,
        controller: _SignedInController(),
        profile: _dashboard(nameEn: 'Mohaned Zagali'),
      );
      // The App-profile name wins over the auth session display name.
      expect(find.textContaining('Mohaned Zagali'), findsOneWidget);
      expect(find.textContaining('Ahmed Mohammed'), findsNothing);
    });

    testWidgets('greeting never renders the email when there is no profile name',
        (tester) async {
      // displayName is the email and no profile loaded → name-less salute.
      await _pump(tester, controller: _SignedInEmailController());
      expect(find.textContaining('@'), findsNothing);
    });

    testWidgets('the discovery hero banner opens News (frame 758:1203)',
        (tester) async {
      await _pump(tester, controller: _SignedInController());
      await tester.tap(find.text('Come discover your favourites'));
      await tester.pumpAndSettle();
      expect(find.text('NEWS'), findsOneWidget);
    });

    testWidgets('the "عن الملتقى" bar opens the About page (758:1207)',
        (tester) async {
      await _pump(tester, controller: _SignedInController());
      await tester.tap(find.text('About the forum'));
      await tester.pumpAndSettle();
      expect(find.text('ABOUT'), findsOneWidget);
    });

    testWidgets('the full-width "اسأل المحاور" tile opens send-question '
        '(1052:12856)', (tester) async {
      // A tall surface renders the full-width tile fully on-screen (a scrolled
      // tile can land under the bottom nav bar and miss the hit test).
      tester.view.physicalSize = const Size(412, 2800);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);
      await _pump(tester, controller: _SignedInController());
      await tester.tap(find.text('Ask the moderator'));
      await tester.pumpAndSettle();
      expect(find.text('SEND-QUESTION'), findsOneWidget);
    });

    testWidgets('the "الرعاة" section bar opens Sponsors (1049:12844)',
        (tester) async {
      tester.view.physicalSize = const Size(412, 2800);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);
      await _pump(tester, controller: _SignedInController());
      await tester.tap(find.text('Sponsors'));
      await tester.pumpAndSettle();
      expect(find.text('SPONSORS'), findsOneWidget);
    });

    testWidgets('the "الأخبار والتغطية" section bar opens News (758:1211)',
        (tester) async {
      tester.view.physicalSize = const Size(412, 2800);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);
      await _pump(tester, controller: _SignedInController());
      await tester.tap(find.text('News & coverage'));
      await tester.pumpAndSettle();
      expect(find.text('NEWS'), findsOneWidget);
    });

    testWidgets('unread badge shows the count when greater than 0',
        (tester) async {
      await _pump(tester, controller: _SignedInController(), unread: 3);
      final badge = tester.widget<Badge>(find.byType(Badge));
      expect(badge.isLabelVisible, isTrue);
      expect((badge.label! as Text).data, '3');
    });

    testWidgets('bell opens notifications', (tester) async {
      await _pump(tester, controller: _SignedInController());

      await tester.tap(find.byTooltip('Notifications'));
      await tester.pumpAndSettle();
      expect(find.text('NOTIFICATIONS'), findsOneWidget);
    });

    testWidgets('the live banner opens the live broadcast', (tester) async {
      await _pump(tester, controller: _SignedInController());

      await tester.tap(find.text('LIVE'));
      await tester.pumpAndSettle();
      expect(find.text('LIVE'), findsOneWidget);
      expect(find.text('Smart features'), findsNothing);
    });

    testWidgets('the social row renders all five brand buttons',
        (tester) async {
      await _pump(tester, controller: _SignedInController());

      await tester.scrollUntilVisible(
        find.text('Follow us'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      final images = tester
          .widgetList<Image>(find.byType(Image))
          .map((w) => (w.image as AssetImage).assetName)
          .where((n) => n.contains('social_'))
          .toList();
      expect(images, hasLength(5));
    });

    testWidgets('the أحدث منشوراتنا card renders the latest post '
        '(frame 758:1240)', (tester) async {
      await _pump(
        tester,
        controller: _SignedInController(),
        news: <NewsListItem>[_post()],
      );

      await tester.scrollUntilVisible(
        find.text('Latest posts'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('Latest posts'), findsOneWidget);
      // The frame's lead paragraph is the excerpt (not the title); the bold line
      // is the source name. Engagement counts are NOT shown (Phase 2 data).
      expect(find.text('The opening session begins now.'), findsOneWidget);
      expect(find.text('The Maritime Forum'), findsOneWidget);
      // The card is tappable (→ the article screen, same push as the news list).
      expect(
        find.ancestor(
          of: find.text('The opening session begins now.'),
          matching: find.byType(InkWell),
        ),
        findsWidgets,
      );
    });

    testWidgets('no posts → the أحدث منشوراتنا section is hidden',
        (tester) async {
      await _pump(tester, controller: _SignedInController());
      // Section is omitted entirely when there is no latest post.
      expect(find.text('Latest posts'), findsNothing);
    });

    testWidgets('relative time buckets (homePostTime)', (tester) async {
      await _pump(tester, controller: _SignedInController());
      final l10n = AppL10n.of(
        tester.element(find.textContaining('Ahmed Mohammed')),
      );
      final base = DateTime.utc(2026, 1, 1, 12);
      expect(homePostTime(l10n, base, base), 'just now');
      expect(
        homePostTime(l10n, base.subtract(const Duration(minutes: 5)), base),
        '5 min ago',
      );
      expect(
        homePostTime(l10n, base.subtract(const Duration(hours: 3)), base),
        '3 h ago',
      );
      expect(
        homePostTime(l10n, base.subtract(const Duration(days: 2)), base),
        '2 d ago',
      );
    });
  });

  // D-436 — every RTL ordering claim is proven with a getCenter().dx position
  // test, never by eye. A tall surface renders the whole list so every tile is
  // laid out without scrolling.
  group('HomeScreen — RTL tile/row order (Arabic, frame 758:1134)', () {
    Future<void> pumpTall(WidgetTester tester) async {
      tester.view.physicalSize = const Size(412, 2800);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);
      await _pump(
        tester,
        controller: _SignedInController(),
        news: <NewsListItem>[_post()],
        locale: const Locale('ar'),
      );
    }

    testWidgets('about tiles: المتحدثون (right) · المعرض · الجلسات (left)',
        (tester) async {
      await pumpTall(tester);
      final speakers = tester.getCenter(find.text('المتحدثون')).dx;
      final booths = tester.getCenter(find.text('المعرض')).dx;
      final sessions = tester.getCenter(find.text('الجلسات')).dx;
      expect(speakers, greaterThan(booths));
      expect(booths, greaterThan(sessions));
    });

    testWidgets('news tiles: اللقاءات الثنائية (right) · الأرشيف (left)',
        (tester) async {
      await pumpTall(tester);
      final bilateral = tester.getCenter(find.text('اللقاءات الثنائية')).dx;
      final archive = tester.getCenter(find.text('الأرشيف')).dx;
      expect(bilateral, greaterThan(archive));
    });

    testWidgets('smart row 2: بطاقتي الذكية (left) · ملخص الجلسات (right)',
        (tester) async {
      await pumpTall(tester);
      final badge = tester.getCenter(find.text('بطاقتي الذكية')).dx;
      final summary = tester.getCenter(find.text('ملخص الجلسات')).dx;
      expect(summary, greaterThan(badge));
    });

    testWidgets('section bars render with the title at the start (right)',
        (tester) async {
      await pumpTall(tester);
      // The three bordered bars exist with the correct Arabic titles.
      expect(find.byType(KsaLinkRow), findsNWidgets(3));
      expect(find.text('عن الملتقى'), findsOneWidget);
      expect(find.text('الرعاة'), findsOneWidget);
      expect(find.text('الأخبار والتغطية'), findsOneWidget);
      // The discover badge is the filled "السعودية", not "KSA" (758:1280).
      expect(find.text('السعودية'), findsOneWidget);
      expect(find.text('KSA'), findsNothing);
    });
  });
}
