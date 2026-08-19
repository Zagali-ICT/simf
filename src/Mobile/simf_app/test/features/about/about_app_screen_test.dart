import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/core/startup/app_version_policy.dart';
import 'package:simf_app/features/about/about_app_screen.dart';

import '../../support/simf_test_scope.dart';

/// A fixed org profile (null = not loaded) so the screen never fires a real
/// fetch / touches prefs.
class _FakeOrgProfileController extends OrgProfileController {
  _FakeOrgProfileController(this.profile);

  final OrgProfile? profile;

  @override
  OrgProfile? build() => profile;
}

/// D-736 — a fixed version-policy payload (or a network failure) so the manual
/// check never fires a real fetch.
class _FakePolicyRepository implements AppVersionPolicyRepository {
  _FakePolicyRepository({this.policy});

  /// Null = the fetch throws (server unreachable).
  final AppVersionPolicy? policy;

  @override
  Future<AppVersionPolicy> fetch() async {
    final value = policy;
    if (value == null) {
      throw Exception('unreachable');
    }
    return value;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  OrgProfile? profile,
  AppVersionPolicy? policy,
  Locale locale = const Locale('en'),
}) async {
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
    simfTestScope(
      overrides: <Override>[
        orgProfileProvider
            .overrideWith(() => _FakeOrgProfileController(profile)),
        installedAppVersionProvider.overrideWithValue('1.0.0'),
        appVersionPolicyRepositoryProvider
            .overrideWithValue(_FakePolicyRepository(policy: policy)),
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

const _androidPolicy = AppVersionPolicy(
  android: PlatformVersionPolicy(
    latestVersion: '2.0.0',
    storeUrl: 'https://play.google.com/store/apps/details?id=sa.simf.app',
  ),
  ios: PlatformVersionPolicy(),
);

const _upToDatePolicy = AppVersionPolicy(
  android: PlatformVersionPolicy(
    latestVersion: '1.0.0',
    storeUrl: 'https://play.google.com/store/apps/details?id=sa.simf.app',
  ),
  ios: PlatformVersionPolicy(),
);

void main() {
  group('AboutAppScreen (D-668)', () {
    testWidgets('shows the app version, release date, organizer + links',
        (tester) async {
      await _pump(tester);

      expect(find.text('About the app'), findsWidgets); // header title
      // D-736 — the REAL installed version (provider-fed), no more literal.
      expect(find.text('1.0.0'), findsOneWidget);
      expect(find.text('06-07-2026'), findsOneWidget);
      expect(find.text('Royal Saudi Naval Forces'), findsOneWidget);
      expect(find.text('Check for updates'), findsOneWidget);
      expect(find.text('Contact us'), findsOneWidget);
      expect(find.text('Terms & conditions'), findsOneWidget);
    });

    testWidgets('the Contact us link opens the contact screen', (tester) async {
      await _pump(tester, policy: _upToDatePolicy);

      await tester.tap(find.text('Contact us'));
      await tester.pumpAndSettle();
      expect(find.text('CONTACT-US'), findsOneWidget);
    });
  });

  group('AboutAppScreen — manual update check (D-736)', () {
    testWidgets('up to date → an explicit confirmation with the version',
        (tester) async {
      await _pump(tester, policy: _upToDatePolicy);

      await tester.tap(find.text('Check for updates'));
      await tester.pumpAndSettle();

      expect(find.text("You're up to date"), findsOneWidget);
      expect(find.text('Current version: 1.0.0'), findsOneWidget);
      await tester.tap(find.text('OK'));
      await tester.pumpAndSettle();
      expect(find.text("You're up to date"), findsNothing);
    });

    testWidgets('newer version on the server → the update offer',
        (tester) async {
      await _pump(tester, policy: _androidPolicy);

      await tester.tap(find.text('Check for updates'));
      await tester.pumpAndSettle();

      expect(find.text('Update available'), findsOneWidget);
      expect(find.textContaining('(2.0.0)'), findsOneWidget);
      expect(find.text('Update now'), findsOneWidget);
      // "Later" just closes the offer (the manual check never snoozes).
      await tester.tap(find.text('Later'));
      await tester.pumpAndSettle();
      expect(find.text('Update available'), findsNothing);
    });

    testWidgets('server unreachable → an honest error, never a fake result',
        (tester) async {
      await _pump(tester); // null policy = the fetch throws

      await tester.tap(find.text('Check for updates'));
      await tester.pumpAndSettle();

      expect(find.text('Something went wrong'), findsOneWidget);
      expect(
        find.textContaining('Could not reach the server'),
        findsOneWidget,
      );
    });

    testWidgets(
        'an unparseable server version → the generic offer, not garbage',
        (tester) async {
      // The forced decision is driven by min=2.0.0; latest="soon" is
      // unparseable and must NOT be rendered as a version (D-736 review fix).
      await _pump(
        tester,
        policy: const AppVersionPolicy(
          android: PlatformVersionPolicy(
            minVersion: '2.0.0',
            latestVersion: 'soon',
            storeUrl:
                'https://play.google.com/store/apps/details?id=sa.simf.app',
          ),
          ios: PlatformVersionPolicy(),
        ),
      );

      await tester.tap(find.text('Check for updates'));
      await tester.pumpAndSettle();

      expect(find.text('Update available'), findsOneWidget);
      // The parsed min (2.0.0) is shown, never the raw "soon".
      expect(find.textContaining('(2.0.0)'), findsOneWidget);
      expect(find.textContaining('soon'), findsNothing);
    });

    testWidgets('Arabic — up to date renders the Arabic confirmation',
        (tester) async {
      await _pump(tester, policy: _upToDatePolicy, locale: const Locale('ar'));
      await tester.tap(find.text('التحقق من التحديثات'));
      await tester.pumpAndSettle();
      expect(find.text('أنت على أحدث إصدار'), findsOneWidget);
      expect(find.text('الإصدار الحالي: 1.0.0'), findsOneWidget);
    });

    testWidgets('Arabic — an update available renders the Arabic offer',
        (tester) async {
      await _pump(tester, policy: _androidPolicy, locale: const Locale('ar'));
      await tester.tap(find.text('التحقق من التحديثات'));
      await tester.pumpAndSettle();
      expect(find.text('يتوفر تحديث'), findsOneWidget);
      expect(find.text('تحديث الآن'), findsOneWidget);
    });

    testWidgets('Arabic — an unreachable server shows the Arabic error',
        (tester) async {
      await _pump(tester, locale: const Locale('ar')); // null policy = throws
      await tester.tap(find.text('التحقق من التحديثات'));
      await tester.pumpAndSettle();
      expect(find.text('حدث خطأ'), findsOneWidget);
    });
  });
}
