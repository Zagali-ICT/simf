import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/about/about_screen.dart';
import 'package:simf_app/features/content/data/content_models.dart';
import 'package:simf_app/features/content/data/content_repository.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _FakeContentRepo implements ContentRepository {
  _FakeContentRepo({this.block, this.status});

  final ContentBlock? block;
  final int? status;

  @override
  Future<ContentBlock> getContentBlock(String key) async {
    if (status != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return block!;
  }
}

/// A stub that pins the shared org-profile value (null → the About screen falls
/// back to its bundled l10n content; a profile → it data-drives the screen).
class _StubOrgProfile extends OrgProfileController {
  _StubOrgProfile(this._value);
  final OrgProfile? _value;
  @override
  OrgProfile? build() => _value;

  @override
  Future<void> warm() async {}
}

OrgProfile _orgProfile() => const OrgProfile(
      name: 'The International Maritime Forum',
      nameArabic: 'الملتقى الدولي البحري',
      title: 'The Saudi Forum',
      titleArabic: 'الملتقى السعودي',
      currentYear: 2026,
      status: 'Open',
      version: '1.0.0',
      contactPhone: '+966 11 000 0000',
      contactEmail: 'info@simf.example',
      contactWebsite: 'https://simf.example',
      social: OrgSocial(),
      aboutItems: <OrgAboutItem>[
        OrgAboutItem(
          title: 'Mission',
          titleArabic: 'الرسالة',
          text: 'Advance maritime dialogue',
          textArabic: 'تعزيز الحوار',
        ),
      ],
      details: <OrgDetail>[
        // A language-neutral value (a year) — no Arabic value; falls back.
        OrgDetail(name: 'Year', nameArabic: 'السنة', value: '2026'),
        // A language-specific value — the Arabic reader sees the Arabic value.
        OrgDetail(
          name: 'Organiser',
          nameArabic: 'الجهة المنظمة',
          value: 'Royal Saudi Naval Forces',
          valueArabic: 'القوات البحرية الملكية السعودية',
        ),
      ],
    );

const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

Future<void> _pump(
  WidgetTester tester,
  ContentRepository repo, {
  Locale locale = const Locale('en'),
  OrgProfile? profile,
}) async {
  // A tall surface so the whole scroll content (header + mission + vision +
  // details + the four themes) lays out — the lazy ListView would otherwise not
  // build the off-screen cards (frame 1116:16448).
  tester.view.physicalSize = const Size(375, 2400);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  final router = GoRouter(
    initialLocation: '/about',
    routes: <RouteBase>[
      GoRoute(
        path: '/about',
        name: RouteNames.aboutForum,
        builder: (_, __) => const AboutScreen(),
      ),
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.home, '/', 'HOME'),
        (RouteNames.sessions, '/sessions', 'SESSIONS'),
        (RouteNames.badge, '/badge', 'BADGE'),
        (RouteNames.venueMap, '/map', 'MAP'),
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
        simfDataConfigProvider.overrideWithValue(_testConfig),
        contentRepositoryProvider.overrideWithValue(repo),
        // Pin the shared profile (null in the fallback cases; a real profile in
        // the data-driven case).
        orgProfileProvider.overrideWith(() => _StubOrgProfile(profile)),
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
  group('AboutScreen (Page 037 — KSA frame 1082:15307)', () {
    testWidgets('renders the heading, the CMS paragraph and the four themes',
        (tester) async {
      await _pump(
        tester,
        _FakeContentRepo(
          block: const ContentBlock(
            key: 'about',
            content: 'About the maritime forum.',
            contentArabic: '',
          ),
        ),
      );
      // Header + static heading.
      expect(find.text('About the forum'), findsOneWidget);
      expect(
        find.text(
          'A Saudi global platform advancing dialogue on maritime-security issues',
        ),
        findsOneWidget,
      );
      // CMS-hydrated paragraph.
      expect(find.text('About the maritime forum.'), findsOneWidget);
      // The themes section + all four numbered cards.
      expect(find.text('Main themes'), findsOneWidget);
      expect(
        find.text('Shifts in the global strategic environment'),
        findsOneWidget,
      );
      for (final n in <String>['01', '02', '03', '04']) {
        expect(find.text(n), findsOneWidget);
      }
    });

    testWidgets('a 404 (unseeded key) falls back to the static paragraph',
        (tester) async {
      await _pump(tester, _FakeContentRepo(status: 404));
      expect(
        find.textContaining('Saudi International Maritime Forum'),
        findsOneWidget,
      );
      // The themes still render — the page always shows the forum content.
      expect(find.text('Main themes'), findsOneWidget);
    });

    testWidgets('a server error also degrades to the static content',
        (tester) async {
      await _pump(tester, _FakeContentRepo(status: 500));
      expect(
        find.textContaining('Saudi International Maritime Forum'),
        findsOneWidget,
      );
      expect(find.text('Challenges and solutions'), findsOneWidget);
    });

    testWidgets('Arabic: the theme number sits to the right of its title',
        (tester) async {
      await _pump(
        tester,
        _FakeContentRepo(status: 404),
        locale: const Locale('ar'),
      );
      final numberX = tester.getCenter(find.text('01')).dx;
      final titleX = tester
          .getCenter(find.text('المتغيرات في البيئة الاستراتيجية العالمية'))
          .dx;
      expect(numberX, greaterThan(titleX));
    });

    testWidgets(
        'D-495 — a profile drives the name, status badge, contact + version',
        (tester) async {
      await _pump(
        tester,
        _FakeContentRepo(status: 404),
        profile: _orgProfile(),
      );

      // Name + title come from the profile (not the l10n default).
      expect(find.text('The International Maritime Forum'), findsOneWidget);
      expect(find.text('The Saudi Forum'), findsOneWidget);
      // The edition status badge: "Open · 2026".
      expect(find.text('Open · 2026'), findsOneWidget);
      // The about-item drives a vision/mission card.
      expect(find.text('Mission'), findsOneWidget);
      // The contact card + a value.
      expect(find.text('Contact'), findsOneWidget);
      expect(find.textContaining('info@simf.example'), findsOneWidget);
      // The version card + its value.
      expect(find.text('System info'), findsOneWidget);
      expect(find.textContaining('1.0.0'), findsOneWidget);
      // D-762 — English reader sees the English detail value.
      expect(find.text('Royal Saudi Naval Forces'), findsOneWidget);
      expect(find.text('القوات البحرية الملكية السعودية'), findsNothing);
    });

    testWidgets('D-762 — Arabic reader sees the Arabic detail value',
        (tester) async {
      await _pump(
        tester,
        _FakeContentRepo(status: 404),
        locale: const Locale('ar'),
        profile: _orgProfile(),
      );

      // A language-specific value switches to its Arabic reading …
      expect(find.text('القوات البحرية الملكية السعودية'), findsOneWidget);
      expect(find.text('Royal Saudi Naval Forces'), findsNothing);
      // … while a language-neutral value (no valueArabic) falls back to Value.
      expect(find.text('2026'), findsWidgets);
    });
  });
}
