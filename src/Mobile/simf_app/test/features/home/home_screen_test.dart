import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/core/site_settings/site_settings.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/banners/data/banner_models.dart';
import 'package:simf_app/features/banners/data/banners_repository.dart';
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

/// A signed-in account whose registration is still pending — it has no
/// permissions, so Home shows the guest layout + the awaiting-approval note.
class _UnapprovedController extends AuthController {
  @override
  AuthState build() => AuthStateSignedIn(
        Session(
          accessToken: 'A',
          refreshToken: 'R',
          accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
          user: CurrentUser(
            id: 'u3',
            email: 'pending@example.sa',
            displayName: 'Pending User',
            appRole: AppRole.visitor,
            preferredLanguage: PreferredLanguage.fromJson('ar'),
            registrationStatus: RegistrationStatus.pending,
          ),
        ),
      );
}

/// A fixed org profile so the social row never fires a real fetch / touches
/// prefs in tests; [profile] null means "not loaded yet".
class _FakeOrgProfileController extends OrgProfileController {
  _FakeOrgProfileController(this.profile);

  final OrgProfile? profile;

  @override
  OrgProfile? build() => profile;
}

/// Five social links all set — the configured case (the follow-us section
/// shows its five buttons).
const _allSocial = OrgSocial(
  x: 'https://x.com/simf',
  instagram: 'https://instagram.com/simf',
  linkedin: 'https://linkedin.com/company/simf',
  youtube: 'https://youtube.com/@simf',
  tiktok: 'https://tiktok.com/@simf',
);

OrgProfile _orgProfile(OrgSocial social, {String? contactWebsite}) => OrgProfile(
      name: '',
      nameArabic: '',
      title: '',
      titleArabic: '',
      currentYear: 0,
      status: '',
      social: social,
      contactWebsite: contactWebsite,
      aboutItems: const <OrgAboutItem>[],
      details: const <OrgDetail>[],
    );

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
  OrgSocial social = _allSocial,
  String? website,
  bool isVip = false,
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
        (RouteNames.requests, '/requests', 'REQUESTS'),
        (RouteNames.meetings, '/meetings', 'MEETINGS'),
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
        // Bi-Meeting rework — the meeting-access flags drive the "اللقاءات الثنائية"
        // tile; overridden so no real getMyProfile() network call happens.
        currentUserMeetingAccessProvider.overrideWith(
          (ref) => MeetingAccess(speaker: isVip, delegation: isVip),
        ),
        simfDataConfigProvider.overrideWithValue(_testConfig),
        notificationsRepositoryProvider
            .overrideWithValue(_FakeNotificationsRepository(unread)),
        newsListProvider.overrideWith((ref) async => news),
        homeProfileProvider.overrideWith((ref) async => profile),
        // The social row reads the CP org profile (warmed at splash) — override
        // it with a fixed profile (social set by default) so no real fetch /
        // prefs access happens.
        orgProfileProvider.overrideWith(
          () => _FakeOrgProfileController(
            _orgProfile(social, contactWebsite: website),
          ),
        ),
        // #43 — no banners by default, so the hero shows the static fallback
        // (no auto-advance timer) and no real GET /app/banners fetch fires.
        bannersProvider.overrideWith((ref) async => const <PublicBannerItem>[]),
        // Build #13 — the Home meet-tile visibility reads site-settings; override
        // it so no real GET /app/site-settings fetch fires (partner directory on).
        siteSettingsProvider.overrideWith(
          (ref) async => const SiteSettings(
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
      // signed-in home greeting header. The shared ☰ stays on the guest top bar.
      expect(find.byTooltip('Notifications'), findsNothing);
      expect(find.byIcon(Icons.menu), findsOneWidget);
      // The language pill was removed from the Home top nav (owner 2026-07-11);
      // language is changed from the More screen's اللغة row now.
      expect(
        find.byKey(const ValueKey<String>('languageToggle')),
        findsNothing,
      );
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

    testWidgets('an unapproved (pending) account sees the guest layout with the '
        'under-review card (registration status + sign out), not the guest '
        'banner or the sign-in button (D-666 / D-668)', (tester) async {
      await _pump(tester, controller: _UnapprovedController());

      // The guest tiles render…
      expect(find.text('Sessions'), findsOneWidget);
      expect(find.text('Speakers'), findsOneWidget);
      // …but the "browsing as a guest, sign in" banner is NOT shown — the
      // account is already logged in (D-666), so that prompt would be wrong.
      expect(find.textContaining('browsing as a guest'), findsNothing);
      // …and the under-review card replaces the sign-in CTA.
      await tester.scrollUntilVisible(
        find.textContaining('awaiting approval'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.textContaining('awaiting approval'), findsOneWidget);
      // A single Registration-status action (D-668 dropped the duplicate
      // Re-check button) plus a sign-out link — never the guest "Sign in".
      expect(find.text('Registration status'), findsOneWidget);
      expect(find.text('Re-check'), findsNothing);
      expect(find.text('Sign out'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Sign in'), findsNothing);
      // It is the guest layout, not the signed-in greeting header.
      expect(find.textContaining('Pending User'), findsNothing);
    });
  });

  group('HomeScreen — signed-in layout (frame 758:1134)', () {
    testWidgets('shows the greeting header, live banner, the section bars and '
        'every tile section', (tester) async {
      // VIP so the "اللقاءات الثنائية" tile is in the section list (D-745).
      await _pump(tester, controller: _SignedInController(), isVip: true);

      // First name only in the greeting (owner 2026-07-21).
      expect(find.textContaining('Ahmed'), findsOneWidget);
      expect(find.byTooltip('Notifications'), findsOneWidget);
      expect(find.text('LIVE'), findsOneWidget);
      // "عن الملتقى" is now a bordered nav row (SimfLinkRow), not a text header.
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
        'Sessions', // الجلسات (about tile → downloads 1388:7621)
        'Ask the moderator', // اسأل المحاور (new full-width tile)
        'News & coverage', // الأخبار والتغطية bar
        'Sponsors', // الرعاة bar
        'Bilateral meetings',
        'Smart features',
        'Session summaries', // ملخص الجلسات (smart tile → summaries 1388:8392)
        'Follow us',
        'Spirit of Saudi',
      ]) {
        expect(seen, contains(section), reason: 'missing section: $section');
      }
      // No guest chrome.
      expect(find.widgetWithText(FilledButton, 'Sign in'), findsNothing);
      expect(find.text('Home • Guest'), findsNothing);
    });

    testWidgets('greeting shows the static welcome word, not the time of day',
        (tester) async {
      await _pump(tester, controller: _SignedInController());
      final l10n = AppL10n.of(
        tester.element(find.textContaining('Ahmed')),
      );

      // Owner 2026-07-21 — the greeting is a time-independent "مرحبًا"/"Welcome"
      // shown above the first name; the morning/evening word is gone.
      expect(find.text(l10n.greetingWelcome), findsOneWidget);
      expect(find.text(l10n.greetingMorning), findsNothing);
      expect(find.text(l10n.greetingEvening), findsNothing);
      // The homeGreeting() helper is retained and still maps hours correctly.
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
      // The App-profile name wins over the auth session display name; the
      // greeting shows the first name only (owner 2026-07-21).
      expect(find.textContaining('Mohaned'), findsOneWidget);
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

    testWidgets('tapping the greeting avatar opens My Area (owner 2026-06-27)',
        (tester) async {
      await _pump(tester, controller: _SignedInController());

      await tester.tap(find.byType(SimfAvatar));
      await tester.pumpAndSettle();
      expect(find.text('MY-AREA'), findsOneWidget);
    });

    testWidgets('the live banner opens the live broadcast', (tester) async {
      await _pump(tester, controller: _SignedInController());

      await tester.tap(find.text('LIVE'));
      await tester.pumpAndSettle();
      expect(find.text('LIVE'), findsOneWidget);
      expect(find.text('Smart features'), findsNothing);
    });

    testWidgets('the social row renders all five brand glyphs (Figma SVGs)',
        (tester) async {
      await _pump(tester, controller: _SignedInController());

      await tester.scrollUntilVisible(
        find.text('Follow us'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      // The social icons are now the exact Figma beige SVGs rendered through
      // SimfSvgIcon (the same pipeline as the About icons), not PNG Images.
      final socials = tester
          .widgetList<SimfSvgIcon>(find.byType(SimfSvgIcon))
          .map((w) => w.asset)
          .where((n) => n.contains('social_'))
          .toList();
      expect(socials, hasLength(5));
    });

    testWidgets('no social set → the تابعنا section is hidden (owner 2026-06-27)',
        (tester) async {
      await _pump(
        tester,
        controller: _SignedInController(),
        social: const OrgSocial(),
      );
      // Scroll to the discover row (just above where تابعنا would be); the
      // follow-us section is hidden, so its header never appears.
      await tester.scrollUntilVisible(
        find.text('Spirit of Saudi'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      expect(find.text('Follow us'), findsNothing);
      final none = tester
          .widgetList<SimfSvgIcon>(find.byType(SimfSvgIcon))
          .where((w) => w.asset.contains('social_'))
          .toList();
      expect(none, isEmpty);
    });

    testWidgets('the org website renders on Home when the CP sets one '
        '(owner 2026-07-08)', (tester) async {
      await _pump(
        tester,
        controller: _SignedInController(),
        website: 'https://simf.example.sa',
      );
      await tester.scrollUntilVisible(
        find.text('Follow us'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      // The website link shows its label + the gold globe glyph below the socials.
      expect(find.text('Website'), findsOneWidget);
      final globe = tester
          .widgetList<SimfSvgIcon>(find.byType(SimfSvgIcon))
          .where((w) => w.asset.contains('auth_globe'))
          .toList();
      expect(globe, hasLength(1));
    });

    testWidgets('no website set → no website link on Home', (tester) async {
      await _pump(tester, controller: _SignedInController());
      await tester.scrollUntilVisible(
        find.text('Follow us'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      expect(find.text('Website'), findsNothing);
    });

    testWidgets('the bilateral-meetings tile opens the VIP meetings page '
        '(1408-9726, D-745)', (tester) async {
      // The tile is VIP-only now — a VIP sees it, and it opens /meetings.
      await _pump(tester, controller: _SignedInController(), isVip: true);
      await tester.scrollUntilVisible(
        find.text('Bilateral meetings'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Bilateral meetings'));
      await tester.pumpAndSettle();
      expect(find.text('MEETINGS'), findsOneWidget);
    });

    testWidgets('the bilateral-meetings tile is hidden for a non-VIP (D-745)',
        (tester) async {
      await _pump(tester, controller: _SignedInController());
      expect(find.text('Bilateral meetings'), findsNothing);
    });

    testWidgets('only the set platforms render (2 of 5 set)', (tester) async {
      await _pump(
        tester,
        controller: _SignedInController(),
        social: const OrgSocial(
          x: 'https://x.com/simf',
          youtube: 'https://youtube.com/@simf',
        ),
      );
      await tester.scrollUntilVisible(
        find.text('Follow us'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('Follow us'), findsOneWidget);
      final two = tester
          .widgetList<SimfSvgIcon>(find.byType(SimfSvgIcon))
          .where((w) => w.asset.contains('social_'))
          .toList();
      expect(two, hasLength(2));
    });

    testWidgets('the ابرز الاحداث carousel renders the post title '
        '(frame 758:1239)', (tester) async {
      await _pump(
        tester,
        controller: _SignedInController(),
        news: <NewsListItem>[_post()],
      );

      await tester.scrollUntilVisible(
        find.text('Highlights'),
        120,
        scrollable: find.byType(Scrollable).first,
      );
      expect(find.text('Highlights'), findsOneWidget);
      // The carousel slide shows the post title (image + text only — the old
      // single card's source chip / excerpt / engagement counts are gone).
      expect(find.text('Forum opens 2026'), findsOneWidget);
      // The slide is tappable (→ the article screen, same push as the list).
      expect(
        find.ancestor(
          of: find.text('Forum opens 2026'),
          matching: find.byType(InkWell),
        ),
        findsWidgets,
      );
    });

    testWidgets('no posts → the ابرز الاحداث section is hidden',
        (tester) async {
      await _pump(tester, controller: _SignedInController());
      // Section is omitted entirely when there is no latest post.
      expect(find.text('Highlights'), findsNothing);
    });

    testWidgets('relative time buckets (homePostTime)', (tester) async {
      await _pump(tester, controller: _SignedInController());
      final l10n = AppL10n.of(
        tester.element(find.textContaining('Ahmed')),
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
        // VIP so the "اللقاءات الثنائية" tile renders in the RTL order checks.
        isVip: true,
      );
    }

    testWidgets(
        'about tiles (4-up): المتحدثون · المعرض · الوفود · '
        'الجلسات (right→left)',
        (tester) async {
      await pumpTall(tester);
      final speakers = tester.getCenter(find.text('المتحدثون')).dx;
      final booths = tester.getCenter(find.text('المعرض')).dx;
      final delegations = tester.getCenter(find.text('الوفود')).dx;
      final sessions = tester.getCenter(find.text('الجلسات')).dx;
      // Right→left order: المتحدثون > المعرض > الوفود > الجلسات.
      expect(speakers, greaterThan(booths));
      expect(booths, greaterThan(delegations));
      expect(delegations, greaterThan(sessions));
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
      final summaries = tester.getCenter(find.text('ملخص الجلسات')).dx;
      expect(summaries, greaterThan(badge));
    });

    testWidgets('section bars render with the title at the start (right)',
        (tester) async {
      await pumpTall(tester);
      // The three bordered bars exist with the correct Arabic titles.
      expect(find.byType(SimfLinkRow), findsNWidgets(3));
      expect(find.text('عن الملتقى'), findsOneWidget);
      expect(find.text('الرعاة'), findsOneWidget);
      expect(find.text('الأخبار والتغطية'), findsOneWidget);
      // The discover badge is the filled "السعودية", not "KSA" (758:1280).
      expect(find.text('السعودية'), findsOneWidget);
      expect(find.text('KSA'), findsNothing);
    });
  });
}
