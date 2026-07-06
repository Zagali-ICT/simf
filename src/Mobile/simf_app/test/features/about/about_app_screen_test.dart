import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/about/about_app_screen.dart';

/// A fixed org profile (null = not loaded) so the screen never fires a real
/// fetch / touches prefs.
class _FakeOrgProfileController extends OrgProfileController {
  _FakeOrgProfileController(this.profile);

  final OrgProfile? profile;

  @override
  OrgProfile? build() => profile;
}

Future<void> _pump(WidgetTester tester, {OrgProfile? profile}) async {
  final router = GoRouter(
    initialLocation: '/about-app',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.aboutApp,
        path: '/about-app',
        builder: (c, s) => const AboutAppScreen(),
      ),
      GoRoute(
        name: RouteNames.contactUs,
        path: '/contact-us',
        builder: (c, s) => const Scaffold(body: Text('CONTACT-US')),
      ),
      GoRoute(
        name: RouteNames.terms,
        path: '/terms',
        builder: (c, s) => const Scaffold(body: Text('TERMS')),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        orgProfileProvider
            .overrideWith(() => _FakeOrgProfileController(profile)),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: const Locale('en'),
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
  group('AboutAppScreen (D-668)', () {
    testWidgets('shows the app version, release date, organizer + links',
        (tester) async {
      await _pump(tester);

      expect(find.text('About the app'), findsWidgets); // header title
      expect(find.text('SIMF 2026 · v1.0.0'), findsOneWidget);
      expect(find.text('2026-07-06'), findsOneWidget);
      expect(find.text('Royal Saudi Naval Forces'), findsOneWidget);
      expect(find.text('Contact us'), findsOneWidget);
      expect(find.text('Terms & conditions'), findsOneWidget);
    });

    testWidgets('the Contact us link opens the contact screen', (tester) async {
      await _pump(tester);

      await tester.tap(find.text('Contact us'));
      await tester.pumpAndSettle();
      expect(find.text('CONTACT-US'), findsOneWidget);
    });
  });
}
