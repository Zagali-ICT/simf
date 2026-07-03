import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Minimal in-memory prefs so a real [LocaleController] can back the
/// language-toggle test without touching the platform store.
class _FakePrefs implements SimfPrefsStorage {
  final Map<String, Object> _store = <String, Object>{};

  @override
  String? getString(String key) {
    final v = _store[key];
    return v is String ? v : null;
  }

  @override
  Future<bool> setString(String key, String value) async {
    _store[key] = value;
    return true;
  }

  @override
  bool? getBool(String key) => null;
  @override
  Future<bool> setBool(String key, bool value) async => true;
  @override
  double? getDouble(String key) => null;
  @override
  Future<bool> setDouble(String key, double value) async => true;
  @override
  int? getInt(String key) => null;
  @override
  Future<bool> setInt(String key, int value) async => true;
  @override
  Future<bool> remove(String key) async {
    _store.remove(key);
    return true;
  }
}

/// Pumps [home] as the `/` route with stub pages for every bottom-nav
/// destination, so taps can assert real navigation.
Future<void> _pump(
  WidgetTester tester,
  Widget home, {
  Locale locale = const Locale('en'),
  List<Override> overrides = const <Override>[],
}) async {
  final router = GoRouter(
    initialLocation: '/',
    routes: <RouteBase>[
      GoRoute(name: RouteNames.home, path: '/', builder: (c, s) => home),
      GoRoute(
        name: RouteNames.sessions,
        path: '/sessions',
        builder: (c, s) => const Scaffold(body: Text('SESSIONS')),
      ),
      GoRoute(
        name: RouteNames.badge,
        path: '/badge',
        builder: (c, s) => const Scaffold(body: Text('BADGE')),
      ),
      GoRoute(
        name: RouteNames.venueMap,
        path: '/map',
        builder: (c, s) => const Scaffold(body: Text('MAP')),
      ),
      GoRoute(
        name: RouteNames.myArea,
        path: '/my-area',
        builder: (c, s) => const Scaffold(body: Text('MY-AREA')),
      ),
    ],
  );

  await tester.pumpWidget(
    // The shared header now carries [SimfLangThemeButtons] (a ConsumerWidget),
    // so the shell needs a ProviderScope just like the running app (main.dart).
    ProviderScope(
      overrides: overrides,
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
  group('SimfBottomNav (KSA Wave-2 shell)', () {
    testWidgets('shows only the active tab label', (tester) async {
      await _pump(
        tester,
        const Scaffold(bottomNavigationBar: SimfBottomNav(current: SimfTab.home)),
      );

      expect(find.text('Home'), findsOneWidget);
      // Inactive destinations are icon-only.
      expect(find.text('Sessions'), findsNothing);
      expect(find.text('Venue map'), findsNothing);
      expect(find.text('Profile'), findsNothing);
    });

    testWidgets('a null current renders the bar with no label at all',
        (tester) async {
      await _pump(
        tester,
        const Scaffold(bottomNavigationBar: SimfBottomNav(current: null)),
      );

      expect(find.text('Home'), findsNothing);
      expect(find.text('Sessions'), findsNothing);
      expect(find.text('Venue map'), findsNothing);
      expect(find.text('Profile'), findsNothing);
      // The centre QR action (exact nav_qr SVG) carries a Semantics label.
      expect(find.bySemanticsLabel('Entry badge'), findsOneWidget);
    });

    testWidgets('sessions icon navigates to /sessions', (tester) async {
      await _pump(
        tester,
        const Scaffold(bottomNavigationBar: SimfBottomNav(current: SimfTab.home)),
      );

      await tester.tap(find.bySemanticsLabel('Sessions'));
      await tester.pumpAndSettle();
      expect(find.text('SESSIONS'), findsOneWidget);
    });

    testWidgets('the profile tab (News slot replacement) opens /my-area',
        (tester) async {
      await _pump(
        tester,
        const Scaffold(bottomNavigationBar: SimfBottomNav(current: SimfTab.home)),
      );

      await tester.tap(find.bySemanticsLabel('Profile'));
      await tester.pumpAndSettle();
      expect(find.text('MY-AREA'), findsOneWidget);
    });

    testWidgets('the gold centre action opens the QR badge', (tester) async {
      await _pump(
        tester,
        const Scaffold(bottomNavigationBar: SimfBottomNav(current: SimfTab.home)),
      );

      await tester.tap(find.bySemanticsLabel('Entry badge'));
      await tester.pumpAndSettle();
      expect(find.text('BADGE'), findsOneWidget);
    });
  });

  group('SimfPageShell', () {
    testWidgets('renders the centred title, fires onBack, carries the nav',
        (tester) async {
      var backs = 0;
      await _pump(
        tester,
        SimfPageShell(
          title: 'My page',
          onBack: () => backs++,
          tab: SimfTab.map,
          body: const Text('BODY'),
        ),
      );

      expect(find.text('My page'), findsOneWidget);
      expect(find.text('BODY'), findsOneWidget);
      expect(find.byType(SimfBottomNav), findsOneWidget);
      // The map tab is active → its label shows.
      expect(find.text('Venue map'), findsOneWidget);

      await tester.tap(find.byType(SimfCircledBackButton));
      expect(backs, 1);
    });

    testWidgets('the action cluster (showHeaderActions:true) carries the '
        'language + inert dark-mode controls', (tester) async {
      await _pump(
        tester,
        SimfPageShell(
          title: 'My page',
          onBack: () {},
          showHeaderActions: true,
          body: const Text('BODY'),
        ),
      );

      // Both shared controls render when the cluster is opted in (Home / guest
      // home); sub-pages default to back + title + line only (owner 2026-06-28).
      expect(find.byIcon(Icons.language), findsOneWidget);
      expect(find.byIcon(Icons.dark_mode), findsOneWidget);

      // The language globe is enabled (toggles AR ↔ EN via LocaleController).
      final langButton = tester.widget<IconButton>(
        find.ancestor(
          of: find.byIcon(Icons.language),
          matching: find.byType(IconButton),
        ),
      );
      expect(langButton.onPressed, isNotNull);

      // Dark mode is intentionally inert — navy-always (owner decision); the
      // icon is shown for parity but is not tappable until a light theme exists.
      final darkButton = tester.widget<IconButton>(
        find.ancestor(
          of: find.byIcon(Icons.dark_mode),
          matching: find.byType(IconButton),
        ),
      );
      expect(darkButton.onPressed, isNull);
    });

    testWidgets('tapping the language globe flips the locale AR → EN and '
        'persists it', (tester) async {
      final prefs = _FakePrefs();
      await _pump(
        tester,
        SimfPageShell(
          title: 'My page',
          onBack: () {},
          showHeaderActions: true,
          body: const Text('BODY'),
        ),
        overrides: <Override>[
          localeControllerProvider.overrideWith(
            () => LocaleController(prefs: prefs),
          ),
        ],
      );

      final container = ProviderScope.containerOf(
        tester.element(find.byType(MaterialApp)),
      );
      // Arabic is the default when nothing is stored (SIMF-MAA-001 §10).
      expect(container.read(localeControllerProvider).languageCode, 'ar');

      await tester.tap(find.byIcon(Icons.language));
      await tester.pumpAndSettle();

      // The globe toggled the locale and persisted the new choice.
      expect(container.read(localeControllerProvider).languageCode, 'en');
      expect(prefs.getString(StorageKeys.preferredLanguage), 'en');
    });

    testWidgets('forced-LTR header (Figma): the back control sits on the LEFT, '
        'the ☰ on the right, even under Arabic/RTL', (tester) async {
      await _pump(
        tester,
        SimfPageShell(
          title: 'صفحة',
          onBack: () {},
          showHeaderActions: true,
          body: const Text('BODY'),
        ),
        locale: const Locale('ar'),
      );

      // Forced LTR (owner 2026-06-28 "match figma in sub page nav"): back on the
      // left, the trailing cluster (ending in ☰) on the right.
      final backDx = tester.getCenter(find.byType(SimfCircledBackButton)).dx;
      final menuDx = tester.getCenter(find.byIcon(Icons.menu)).dx;
      expect(backDx, lessThan(menuDx));
    });

    testWidgets('the standard sub-page header is back + title only — no action '
        'cluster by default (Figma 758-1469, owner 2026-06-28)', (tester) async {
      await _pump(
        tester,
        SimfPageShell(title: 'My page', onBack: () {}, body: const Text('BODY')),
      );
      // The Figma sub-page nav carries no bell / language / theme / menu.
      expect(find.byIcon(Icons.notifications_none_outlined), findsNothing);
      expect(find.byIcon(Icons.language), findsNothing);
      expect(find.byIcon(Icons.menu), findsNothing);
      expect(find.byIcon(Icons.dark_mode), findsNothing);

      // Opting in (Home / guest home) brings the cluster back, with the bell.
      await _pump(
        tester,
        SimfPageShell(
          title: 'My page',
          onBack: () {},
          showHeaderActions: true,
          body: const Text('BODY'),
        ),
      );
      expect(find.byIcon(Icons.notifications_none_outlined), findsOneWidget);
    });

    testWidgets('showNotificationsBell:false hides the bell (guest home)',
        (tester) async {
      await _pump(
        tester,
        const SimfPageShell(
          title: 'Guest',
          showNotificationsBell: false,
          body: Text('BODY'),
        ),
      );
      // The guest home (frame 758:2910) carries no bell — a guest has no
      // personal notifications.
      expect(find.byIcon(Icons.notifications_none_outlined), findsNothing);
    });

    testWidgets('no onBack → no back button; custom header replaces the row',
        (tester) async {
      await _pump(
        tester,
        const SimfPageShell(
          header: Text('CUSTOM-HEADER'),
          body: SizedBox.shrink(),
        ),
      );

      expect(find.byType(SimfCircledBackButton), findsNothing);
      expect(find.text('CUSTOM-HEADER'), findsOneWidget);
    });

    testWidgets('no header, title or onBack → the header row collapses',
        (tester) async {
      await _pump(tester, const SimfPageShell(body: Text('FULL-BLEED')));

      expect(find.byType(SimfCircledBackButton), findsNothing);
      expect(find.text('FULL-BLEED'), findsOneWidget);
      // The bar is always carried by the page.
      expect(find.byType(SimfBottomNav), findsOneWidget);
    });
  });

  group('SimfNavTile / SimfStatTile / SimfListRow', () {
    testWidgets('enabled tile fires onTap', (tester) async {
      var taps = 0;
      await _pump(
        tester,
        Scaffold(
          body: SimfNavTile(
            label: 'Speakers',
            icon: Icons.mic_none,
            onTap: () => taps++,
          ),
        ),
      );

      await tester.tap(find.text('Speakers'));
      expect(taps, 1);
    });

    testWidgets('disabled tile renders the locked palette and ignores taps',
        (tester) async {
      var taps = 0;
      await _pump(
        tester,
        Scaffold(
          body: SimfNavTile(
            label: 'My badge',
            icon: Icons.badge_outlined,
            enabled: false,
            onTap: () => taps++,
          ),
        ),
      );

      await tester.tap(find.text('My badge'));
      expect(taps, 0);
      final material = tester.widget<Material>(
        find.ancestor(
          of: find.text('My badge'),
          matching: find.byType(Material),
        ).first,
      );
      expect(material.color, SimfTokens.navyDisabled);
    });

    testWidgets('stat tile shows the gold value over its label',
        (tester) async {
      await _pump(
        tester,
        const Scaffold(body: SimfStatTile(value: 6, label: 'Booked')),
      );

      expect(find.text('6'), findsOneWidget);
      expect(find.text('Booked'), findsOneWidget);
    });

    testWidgets('stat tile fires onTap when given one', (tester) async {
      var taps = 0;
      await _pump(
        tester,
        Scaffold(
          body: SimfStatTile(value: 6, label: 'Booked', onTap: () => taps++),
        ),
      );

      await tester.tap(find.text('Booked'));
      await tester.pumpAndSettle();
      expect(taps, 1);
    });

    testWidgets('list row shows title + subtitle + badge and fires onTap',
        (tester) async {
      var taps = 0;
      await _pump(
        tester,
        Scaffold(
          body: SimfListRow(
            title: 'FAQ',
            subtitle: 'Venue & event info',
            badge: const Icon(Icons.help_outline),
            onTap: () => taps++,
          ),
        ),
      );

      expect(find.text('FAQ'), findsOneWidget);
      expect(find.text('Venue & event info'), findsOneWidget);
      expect(find.byIcon(Icons.help_outline), findsOneWidget);
      await tester.tap(find.text('FAQ'));
      expect(taps, 1);
    });

    testWidgets('badgeOutlined draws a gold border instead of a gold fill',
        (tester) async {
      await _pump(
        tester,
        Scaffold(
          body: Column(
            children: <Widget>[
              SimfListRow(title: 'Filled', badge: const Text('A'), onTap: () {}),
              SimfListRow(
                title: 'Outlined',
                badge: const Text('B'),
                badgeOutlined: true,
                onTap: () {},
              ),
            ],
          ),
        ),
      );

      BoxDecoration badgeDecoration(String badgeText) => tester
          .widget<Container>(
            find
                .ancestor(
                  of: find.text(badgeText),
                  matching: find.byType(Container),
                )
                .first,
          )
          .decoration! as BoxDecoration;

      // The default badge is a solid gold fill with no border.
      final filled = badgeDecoration('A');
      expect(filled.color, SimfTokens.accent);
      expect(filled.border, isNull);

      // The outlined badge (guest FAQ / روح السعودية, frame 758:2910) is a gold
      // hairline over the card — no fill.
      final outlined = badgeDecoration('B');
      expect(outlined.color, isNull);
      expect(outlined.border, isNotNull);
    });
  });
}
