import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_app/features/myarea/my_area_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

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
              startUtc: DateTime.utc(2026, 9, 13, 8),
              titleEn: 'Opening',
              titleAr: 'الافتتاح',
              status: 'Approved',
              sessionId: 's1',
            ),
          ],
    );

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
    testWidgets('approved visitor sees the identity card, tiles, stats and '
        'schedule', (tester) async {
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: _FakeMyAreaRepository(dashboard: _dashboard()),
      );

      expect(find.text('Raed Al-Salem'), findsOneWidget);
      expect(find.text('#ABC123'), findsOneWidget);
      expect(find.textContaining('VIP'), findsOneWidget);
      expect(find.text('Share my profile'), findsOneWidget);
      expect(find.text('Share contact'), findsOneWidget);
      expect(find.text('Statistics'), findsOneWidget); // الإحصائيات header
      expect(find.text('6'), findsOneWidget); // booked-sessions stat
      expect(find.text('3'), findsOneWidget); // meetings stat
      await _scrollTo(tester, find.text('Opening'));
      expect(find.text('Opening'), findsOneWidget);
      await _scrollTo(tester, find.text('My smart badge'));
      expect(find.text('My smart badge'), findsOneWidget);
      // Photos-only profile edit (two-photo split): the Update ID photo action.
      await _scrollTo(tester, find.text('Update ID photo'));
      expect(find.text('Update ID photo'), findsOneWidget);
      // Language / theme / calendar export / sign-out moved to the shell's
      // side drawer (D-396) — they must NOT be on the profile page anymore.
      expect(find.text('العربية · English'), findsNothing);
      expect(find.text('Light / dark mode'), findsNothing);
      expect(find.text('Sign out'), findsNothing);
    });

    testWidgets('the share-my-profile tile opens the contact-QR screen',
        (tester) async {
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: _FakeMyAreaRepository(dashboard: _dashboard()),
      );

      await tester.tap(find.text('Share my profile'));
      await tester.pumpAndSettle();
      expect(find.text('SHARE-MY-CONTACT'), findsOneWidget);
    });

    testWidgets('the dashboard avatar shows the tap-to-change camera affordance',
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
                startUtc: DateTime.utc(2026, 9, 13, 8),
                titleEn: 'Opening',
                titleAr: 'الافتتاح',
                status: 'Approved',
                sessionId: 's1',
              ),
              MyAreaScheduleItem(
                kind: 'Meeting',
                startUtc: DateTime.utc(2026, 9, 13, 10),
                titleEn: 'Dr Ibrahim',
                titleAr: 'مقابلة د. ابراهيم',
                status: 'Approved',
              ),
            ],
          ),
        ),
        locale: const Locale('ar'),
      );
      // The gold "جلسات" sub-header (the stat tile is "جلسات محفوظة", distinct).
      expect(find.text('جلسات'), findsOneWidget);
      expect(find.text('مقابلة د. ابراهيم'), findsOneWidget);
      // The sessions group sits above the meetings group.
      final session = tester.getCenter(find.text('الافتتاح')).dy;
      final meeting = tester.getCenter(find.text('مقابلة د. ابراهيم')).dy;
      expect(session, lessThan(meeting));
    });

    testWidgets('share pills: مشاركة جهة اتصال (right) · مشاركة ملفي (left) '
        '(758:1305)', (tester) async {
      await _pump(
        tester,
        controller: _AuthController(RegistrationStatus.approved),
        repo: _FakeMyAreaRepository(dashboard: _dashboard()),
        locale: const Locale('ar'),
      );
      final contact = tester.getCenter(find.text('مشاركة جهة اتصال')).dx;
      final profile = tester.getCenter(find.text('مشاركة ملفي')).dx;
      expect(contact, greaterThan(profile));
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
      expect(find.text('6'), findsNothing); // counters hidden
      expect(find.text('My smart badge'), findsNothing);
      // No photo-change affordance on the limited card (onAvatarTap is null).
      expect(find.byIcon(Icons.photo_camera_outlined), findsNothing);
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
