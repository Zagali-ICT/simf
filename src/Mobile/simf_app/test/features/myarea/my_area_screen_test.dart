import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_app/features/myarea/my_area_screen.dart';
import 'package:simf_app/features/sessions/data/session_favourites.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

CurrentUser _user(RegistrationStatus status) => CurrentUser(
      id: 'u1',
      email: 'v@example.sa',
      displayName: 'Raed Al-Salem',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('ar'),
      registrationStatus: status,
    );

Session _session(RegistrationStatus status) => Session(
      accessToken: 'A',
      refreshToken: 'R',
      accessTokenExpiresAt: DateTime.now().add(const Duration(minutes: 30)),
      user: _user(status),
    );

class _AuthController extends AuthController {
  _AuthController(this.status);

  final RegistrationStatus status;

  @override
  AuthState build() => AuthStateSignedIn(_session(status));
}

/// A TRUE guest — no account at all (BUG-013). The profile tab is reachable
/// this way because the bottom nav switches tabs inside the shell, so the
/// router's auth redirect never runs.
class _SignedOutAuthController extends AuthController {
  @override
  AuthState build() => const AuthStateSignedOut();
}

MyAreaDashboard _dashboard({List<MyAreaScheduleItem>? schedule}) =>
    MyAreaDashboard(
      identity: const MyAreaIdentity(
        fullNameAr: 'رائد السالم',
        fullNameEn: 'Raed Al-Salem',
        qrId: 'ABC123',
        tierNameEn: 'VIP',
        tierNameAr: 'VIP',
      ),
      counters: const MyAreaCounters(bookedSessionsCount: 6, meetingsCount: 3),
      todaySchedule: schedule ??
          <MyAreaScheduleItem>[
            MyAreaScheduleItem(
              kind: 'Session',
              start: DateTime.utc(2026, 9, 13, 8),
              titleEn: 'Opening',
              titleAr: 'الافتتاح',
              status: 'Approved',
              sessionId: 's1',
            ),
          ],
    );

/// Two favourited sessions → the جلسات محفوظة stat tile shows 2 (display-only).
class _FakeFavourites extends SessionFavouritesController {
  @override
  Future<Set<String>> build() async => <String>{'s1', 's2'};
}

class _FakeMyAreaRepository implements MyAreaRepository {
  _FakeMyAreaRepository({this.dashboard, this.fail = false, this.status = 500});

  final MyAreaDashboard? dashboard;
  final bool fail;
  final int status;
  int dashboardCalls = 0;

  @override
  Future<MyAreaDashboard> getDashboard() async {
    dashboardCalls++;
    if (fail) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return dashboard!;
  }

  @override
  Future<String> getContactCardVcf() async => 'BEGIN:VCARD\r\nEND:VCARD\r\n';

  @override
  Future<String> getCalendarIcs() async =>
      'BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n';

  @override
  Future<bool> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) async =>
      true;
}

Future<void> _pump(
  WidgetTester tester, {
  required AuthController controller,
  MyAreaRepository? repo,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/my-area',
    routes: <RouteBase>[
      GoRoute(
        name: RouteNames.myArea,
        path: '/my-area',
        builder: (c, s) => const MyAreaScreen(),
      ),
      GoRoute(
        name: RouteNames.sessionDetail,
        path: '/sessions/:sessionId',
        builder: (c, s) =>
            Scaffold(body: Text('SESSION ${s.pathParameters['sessionId']}')),
      ),
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.badge, '/badge', 'BADGE'),
        (RouteNames.more, '/more', 'MORE'),
        (RouteNames.shareMyContact, '/contacts/share', 'SHARE-MY-CONTACT'),
        (RouteNames.home, '/', 'HOME'),
        (RouteNames.sessions, '/sessions', 'SESSIONS'),
        (RouteNames.venueMap, '/map', 'MAP'),
        (RouteNames.signIn, '/sign-in', 'SIGN-IN'),
        (RouteNames.signUpForm, '/sign-up', 'SIGN-UP'),
      ])
        GoRoute(
          name: name,
          path: path,
          builder: (c, s) => Scaffold(body: Text(label)),
        ),
    ],
  );

  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        authControllerProvider.overrideWith(() => controller),
        sessionFavouritesProvider.overrideWith(_FakeFavourites.new),
        if (repo != null) myAreaRepositoryProvider.overrideWithValue(repo),
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

Future<void> _scrollTo(WidgetTester tester, Finder finder) async {
  await tester.scrollUntilVisible(
    finder,
    120,
    scrollable: find.byType(Scrollable).first,
  );
  await tester.pumpAndSettle();
}

void main() {
  group('MyAreaScreen (Page 014 — KSA frame 213:963)', () {
    testWidgets(
        'approved visitor sees the identity card, tiles and '
        'schedule', (tester) async {
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: _FakeMyAreaRepository(dashboard: _dashboard()),
      );

      expect(find.text('Raed Al-Salem'), findsOneWidget);
      expect(find.text('#ABC123'), findsOneWidget);
      expect(find.textContaining('VIP'), findsOneWidget);
      // #21 — Share my profile (مشاركة ملفي) was dropped as a duplicate;
      // only Share contact (مشاركة جهة اتصال) remains.
      expect(find.text('Share my profile'), findsNothing);
      expect(find.text('Share contact'), findsOneWidget);
      // D-653 — الإحصائيات restored display-only (not tappable): the مقابلات
      // count (3, dashboard) + the جلسات محفوظة count (2, favourited set).
      expect(find.text('Statistics'), findsOneWidget);
      expect(find.text('Meetings'), findsOneWidget);
      expect(find.text('Saved sessions'), findsOneWidget);
      expect(find.text('3'), findsOneWidget); // meetings count
      expect(find.text('2'), findsOneWidget); // saved (favourited) count
      await _scrollTo(tester, find.text('Opening'));
      expect(find.text('Opening'), findsOneWidget);
      await _scrollTo(tester, find.text('My smart badge'));
      expect(find.text('My smart badge'), findsOneWidget);
      // D-654 — the "Update ID photo" action was removed from My Area (owner).
      expect(find.text('Update ID photo'), findsNothing);
      // Language / theme / calendar export / sign-out moved to the shell's
      // side drawer (D-396) — they must NOT be on the profile page anymore.
      expect(find.text('العربية · English'), findsNothing);
      expect(find.text('Light / dark mode'), findsNothing);
      expect(find.text('Sign out'), findsNothing);
    });

    // #21 — the "Share contact" tile used to fire a native .vcf share
    // sheet; the owner re-pointed it to the same in-app QR screen the
    // share-my-profile tile opens, so it now routes to shareMyContact.
    testWidgets('the share-contact tile opens the contact-QR screen (#21)',
        (tester) async {
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: _FakeMyAreaRepository(dashboard: _dashboard()),
      );

      await tester.tap(find.text('Share contact'));
      await tester.pumpAndSettle();
      expect(find.text('SHARE-MY-CONTACT'), findsOneWidget);
    });

    testWidgets(
        'the dashboard avatar shows the tap-to-change camera affordance',
        (tester) async {
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: _FakeMyAreaRepository(dashboard: _dashboard()),
      );

      // The identity-card avatar carries the camera badge (D-401) — the photo
      // is changeable on tap. (The picker itself is a platform channel and is
      // not driven here.)
      expect(find.byIcon(Icons.photo_camera_outlined), findsOneWidget);
    });

    testWidgets('empty schedule shows the no-items placeholder',
        (tester) async {
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: _FakeMyAreaRepository(
          dashboard: _dashboard(schedule: const <MyAreaScheduleItem>[]),
        ),
      );

      await _scrollTo(tester, find.text('No items today'));
      expect(find.text('No items today'), findsOneWidget);
    });

    testWidgets('جدولي اليوم splits into جلسات then مقابلات groups (758:1283)',
        (tester) async {
      tester.view.physicalSize = const Size(412, 1800);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: _FakeMyAreaRepository(
          dashboard: _dashboard(
            schedule: <MyAreaScheduleItem>[
              MyAreaScheduleItem(
                kind: 'Session',
                start: DateTime.utc(2026, 9, 13, 8),
                titleEn: 'Opening',
                titleAr: 'الافتتاح',
                status: 'Approved',
                sessionId: 's1',
              ),
              MyAreaScheduleItem(
                kind: 'Meeting',
                start: DateTime.utc(2026, 9, 13, 10),
                titleEn: 'Dr Ibrahim',
                titleAr: 'مقابلة د. ابراهيم',
                status: 'Approved',
              ),
            ],
          ),
        ),
        locale: const Locale('ar'),
      );
      // The gold "جلسات" schedule sub-header.
      expect(find.text('جلسات'), findsOneWidget);
      expect(find.text('مقابلة د. ابراهيم'), findsOneWidget);
      // The sessions group sits above the meetings group.
      final session = tester.getCenter(find.text('الافتتاح')).dy;
      final meeting = tester.getCenter(find.text('مقابلة د. ابراهيم')).dy;
      expect(session, lessThan(meeting));
    });

    testWidgets('only مشاركة جهة اتصال remains, مشاركة ملفي dropped (#21)',
        (tester) async {
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: _FakeMyAreaRepository(dashboard: _dashboard()),
        locale: const Locale('ar'),
      );
      // #21 — the duplicate مشاركة ملفي pill was removed; only the single
      // مشاركة جهة اتصال pill remains as the share tile.
      expect(find.text('مشاركة جهة اتصال'), findsOneWidget);
      expect(find.text('مشاركة ملفي'), findsNothing);
    });

    testWidgets('pending account shows the limited card, no dashboard call',
        (tester) async {
      final repo = _FakeMyAreaRepository(dashboard: _dashboard());
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.pending),
        repo: repo,
      );

      expect(repo.dashboardCalls, 0); // approved-only endpoint not called (L-5)
      expect(find.text('Raed Al-Salem'), findsOneWidget); // cached name
      expect(find.textContaining('under review'), findsOneWidget);
      expect(find.text('My smart badge'), findsNothing);
      // No photo-change affordance on the limited card (onAvatarTap is null).
      expect(find.byIcon(Icons.photo_camera_outlined), findsNothing);
    });

    testWidgets(
        'BUG-013 — a TRUE guest gets the guest copy and a working '
        'sign-in CTA, never the under-review copy', (tester) async {
      final repo = _FakeMyAreaRepository(dashboard: _dashboard());
      await _pump(
        tester,
        controller: _SignedOutAuthController(),
        repo: repo,
      );

      expect(repo.dashboardCalls, 0);
      // The "under review" wording describes a registration a guest never
      // submitted — it must NOT be shown.
      expect(find.textContaining('under review'), findsNothing);
      expect(
        find.text(
          'Sign in or create an account to see your profile and schedule.',
        ),
        findsOneWidget,
      );
      expect(find.widgetWithText(TextButton, 'Create account'), findsOneWidget);

      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();
      expect(find.text('SIGN-IN'), findsOneWidget);
    });

    testWidgets('a 403 for an approved user falls back to the limited card',
        (tester) async {
      final repo = _FakeMyAreaRepository(fail: true, status: 403);
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: repo,
      );

      expect(repo.dashboardCalls, 1);
      expect(find.textContaining('under review'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, 'Retry'), findsNothing);
    });

    testWidgets('a load failure shows the error + retry, which re-fetches',
        (tester) async {
      final repo = _FakeMyAreaRepository(fail: true);
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: repo,
      );

      expect(find.text('Could not load your area.'), findsOneWidget);
      final retry = find.widgetWithText(FilledButton, 'Retry');
      expect(retry, findsOneWidget);

      await tester.tap(retry);
      await tester.pumpAndSettle();
      expect(repo.dashboardCalls, greaterThanOrEqualTo(2));
    });

    testWidgets('tapping a session row routes to session detail',
        (tester) async {
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: _FakeMyAreaRepository(dashboard: _dashboard()),
      );

      await _scrollTo(tester, find.text('Opening'));
      await tester.tap(find.text('Opening'));
      await tester.pumpAndSettle();
      expect(find.text('SESSION s1'), findsOneWidget);
    });

    testWidgets('renders right-to-left in Arabic', (tester) async {
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: _FakeMyAreaRepository(dashboard: _dashboard()),
        locale: const Locale('ar'),
      );

      // Header matches the frame 758:1283 + the bottom-nav label.
      expect(find.text('الملف الشخصى'), findsWidgets);
      await _scrollTo(tester, find.text('الافتتاح'));
      expect(
        Directionality.of(tester.element(find.text('الافتتاح'))),
        TextDirection.rtl,
      );
    });
  });
}
