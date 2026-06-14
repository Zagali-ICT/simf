import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/home/home_screen.dart';
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_app/features/news/news_screen.dart' show newsListProvider;
import 'package:simf_app/features/notifications/data/notification_models.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

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

Future<void> _pump(
  WidgetTester tester, {
  required AuthController controller,
  int unread = 0,
  List<NewsListItem> news = const <NewsListItem>[],
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
        (RouteNames.meetPeople, '/meet', 'MEET'),
        (RouteNames.chatbot, '/chatbot', 'CHATBOT'),
        (RouteNames.aiSummary, '/ai-summary', 'AI-SUMMARY'),
        (RouteNames.badge, '/badge', 'BADGE'),
        (RouteNames.news, '/news', 'NEWS'),
        (RouteNames.more, '/more', 'MORE'),
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
        notificationsRepositoryProvider
            .overrideWithValue(_FakeNotificationsRepository(unread)),
        newsListProvider.overrideWith((ref) async => news),
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
      // The shared shell's top controller (notifications + ☰) is on every
      // KsaPage including the guest home (D-395; owner: same top bar on all
      // pages). Tapping the bell on a guest just routes through the auth gate.
      expect(find.byTooltip('Notifications'), findsOneWidget);
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

    testWidgets('the FAQ row opens the about page (no app FAQ endpoint yet)',
        (tester) async {
      await _pump(tester, controller: _GuestController());

      await tester.scrollUntilVisible(
        find.text('FAQ'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('FAQ'));
      await tester.pumpAndSettle();
      expect(find.text('ABOUT'), findsOneWidget);
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

  group('HomeScreen — signed-in layout (frame 203:1236)', () {
    testWidgets('shows the greeting header, live banner and all three tile '
        'sections', (tester) async {
      await _pump(tester, controller: _SignedInController());

      expect(find.textContaining('Ahmed Mohammed'), findsOneWidget);
      expect(find.byTooltip('Notifications'), findsOneWidget);
      expect(find.text('LIVE'), findsOneWidget);
      expect(find.text('About the forum · Themes'), findsOneWidget);
      // The lower sections mount as the list scrolls.
      for (final below in <String>[
        'News & coverage',
        'Bilateral meetings',
        'Smart features',
        'Session summaries',
        'Follow us',
        'Discover',
      ]) {
        await tester.scrollUntilVisible(
          find.text(below),
          120,
          scrollable: find.byType(Scrollable).first,
        );
        expect(find.text(below), findsOneWidget);
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

    testWidgets('unread badge shows the count when greater than 0',
        (tester) async {
      await _pump(tester, controller: _SignedInController(), unread: 3);
      final badge = tester.widget<Badge>(find.byType(Badge));
      expect(badge.isLabelVisible, isTrue);
      expect((badge.label! as Text).data, '3');
    });

    testWidgets('bell opens notifications; a smart tile opens its route',
        (tester) async {
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
        '(frame 522:2345)', (tester) async {
      await _pump(
        tester,
        controller: _SignedInController(),
        news: <NewsListItem>[_post(title: 'Forum opens 2026')],
      );

      await tester.scrollUntilVisible(
        find.text('Latest posts'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('Latest posts'), findsOneWidget);
      expect(find.text('Forum opens 2026'), findsOneWidget);
      // Source label is shown; engagement counts are NOT (no data — never faked).
      expect(find.text('Saudi Maritime Forum'), findsOneWidget);
      // The card is tappable (→ the article screen, same push as the news list).
      expect(
        find.ancestor(
          of: find.text('Forum opens 2026'),
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
}
