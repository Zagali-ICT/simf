import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/ksa_shell.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';

/// Pumps [home] as the `/` route with stub pages for every bottom-nav
/// destination, so taps can assert real navigation.
Future<void> _pump(
  WidgetTester tester,
  Widget home, {
  Locale locale = const Locale('en'),
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
    MaterialApp.router(
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
      expect(find.text('Agenda'), findsNothing);
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
      expect(find.text('Agenda'), findsNothing);
      expect(find.text('Venue map'), findsNothing);
      expect(find.text('Profile'), findsNothing);
      expect(find.byIcon(Icons.qr_code_2_rounded), findsOneWidget);
    });

    testWidgets('agenda icon navigates to /sessions', (tester) async {
      await _pump(
        tester,
        const Scaffold(bottomNavigationBar: SimfBottomNav(current: SimfTab.home)),
      );

      await tester.tap(find.byIcon(Icons.calendar_today_outlined));
      await tester.pumpAndSettle();
      expect(find.text('SESSIONS'), findsOneWidget);
    });

    testWidgets('the profile tab (News slot replacement) opens /my-area',
        (tester) async {
      await _pump(
        tester,
        const Scaffold(bottomNavigationBar: SimfBottomNav(current: SimfTab.home)),
      );

      await tester.tap(find.byIcon(Icons.person_outline));
      await tester.pumpAndSettle();
      expect(find.text('MY-AREA'), findsOneWidget);
    });

    testWidgets('the gold centre action opens the QR badge', (tester) async {
      await _pump(
        tester,
        const Scaffold(bottomNavigationBar: SimfBottomNav(current: SimfTab.home)),
      );

      await tester.tap(find.byIcon(Icons.qr_code_2_rounded));
      await tester.pumpAndSettle();
      expect(find.text('BADGE'), findsOneWidget);
    });
  });

  group('KsaPage', () {
    testWidgets('renders the centred title, fires onBack, carries the nav',
        (tester) async {
      var backs = 0;
      await _pump(
        tester,
        KsaPage(
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

      await tester.tap(find.byType(KsaBackButton));
      expect(backs, 1);
    });

    testWidgets('no onBack → no back button; custom header replaces the row',
        (tester) async {
      await _pump(
        tester,
        const KsaPage(
          header: Text('CUSTOM-HEADER'),
          body: SizedBox.shrink(),
        ),
      );

      expect(find.byType(KsaBackButton), findsNothing);
      expect(find.text('CUSTOM-HEADER'), findsOneWidget);
    });

    testWidgets('no header, title or onBack → the header row collapses',
        (tester) async {
      await _pump(tester, const KsaPage(body: Text('FULL-BLEED')));

      expect(find.byType(KsaBackButton), findsNothing);
      expect(find.text('FULL-BLEED'), findsOneWidget);
      // The bar is always carried by the page.
      expect(find.byType(SimfBottomNav), findsOneWidget);
    });
  });

  group('KsaNavTile / KsaStatTile / KsaListRow', () {
    testWidgets('enabled tile fires onTap', (tester) async {
      var taps = 0;
      await _pump(
        tester,
        Scaffold(
          body: KsaNavTile(
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
          body: KsaNavTile(
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
        const Scaffold(body: KsaStatTile(value: 6, label: 'Booked')),
      );

      expect(find.text('6'), findsOneWidget);
      expect(find.text('Booked'), findsOneWidget);
    });

    testWidgets('list row shows title + subtitle + badge and fires onTap',
        (tester) async {
      var taps = 0;
      await _pump(
        tester,
        Scaffold(
          body: KsaListRow(
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
  });
}
