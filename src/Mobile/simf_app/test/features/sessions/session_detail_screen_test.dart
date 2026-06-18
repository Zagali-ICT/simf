import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/sessions/data/session_calendar.dart';
import 'package:simf_app/features/sessions/data/session_detail_repository.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/session_detail_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

SessionDetail _detail() => SessionDetail(
      id: 's1',
      code: 'OP-1',
      title: 'Opening',
      titleArabic: 'الافتتاح',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      startUtc: DateTime.utc(2026, 11, 23, 6),
      endUtc: DateTime.utc(2026, 11, 23, 7),
      speakers: const <SessionSpeaker>[
        SessionSpeaker(
          id: 'sp1',
          name: 'Dr Reef',
          nameArabic: 'د. ريف',
          displayOrder: 0,
          role: SessionSpeakerRole.speaker,
          title: 'Chief Scientist',
          countryNameEn: 'Saudi Arabia',
        ),
      ],
      description: 'Welcome address',
      categoryName: 'Main Session',
      categoryNameArabic: 'جلسة رئيسية',
    );

const _seat = MySeat(reservationId: 'r1', rowLabel: 'B', seatNumber: 12);

CurrentUser _visitor() => CurrentUser(
      id: 'u1',
      email: 'visitor@example.sa',
      displayName: 'Visitor',
      appRole: AppRole.visitor,
      preferredLanguage: PreferredLanguage.fromJson('en'),
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

class _FakeDetailRepo implements SessionDetailRepository {
  _FakeDetailRepo({this.detail, this.seat, this.detailStatus});

  final SessionDetail? detail;
  final MySeat? seat;
  final int? detailStatus;
  int detailCalls = 0;

  @override
  Future<SessionDetail> getDetail(String sessionId) async {
    detailCalls++;
    if (detailStatus != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: detailStatus,
      );
    }
    return detail!;
  }

  @override
  Future<MySeat?> getMySeat(String sessionId) async => seat;
}

class _FakeCalendar implements SessionCalendar {
  @override
  Future<bool> addSession(SessionDetail detail, {required bool isArabic}) async =>
      true;
}

Future<void> _pump(
  WidgetTester tester, {
  required SessionDetailRepository repo,
  required AuthController controller,
  SessionCalendar? calendar,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/sessions/s1',
    routes: <RouteBase>[
      GoRoute(
        path: '/sessions/:sessionId',
        name: RouteNames.sessionDetail,
        builder: (_, state) => SessionDetailScreen(
          sessionId: state.pathParameters['sessionId'] ?? '',
        ),
      ),
      GoRoute(
        path: '/speakers/:speakerId',
        name: RouteNames.speakerProfile,
        builder: (_, state) =>
            Scaffold(body: Text('SPEAKER ${state.pathParameters['speakerId']}')),
      ),
      GoRoute(
        path: '/sessions/:sessionId/my-seat',
        name: RouteNames.mySeat,
        builder: (_, __) => const Scaffold(body: Text('MY-SEAT')),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        sessionDetailRepositoryProvider.overrideWithValue(repo),
        sessionCalendarProvider
            .overrideWithValue(calendar ?? _FakeCalendar()),
        authControllerProvider.overrideWith(() => controller),
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
  group('SessionDetailScreen (Page 017)', () {
    testWidgets('renders the KSA detail (header card, description, speaker, '
        'tags, CTAs)', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        controller: _GuestController(),
      );

      // Header card: the centred title chrome + the title/code + meta.
      expect(find.text('Session detail'), findsOneWidget);
      expect(find.text('Opening'), findsOneWidget);
      expect(find.text('OP-1'), findsOneWidget); // the gold index badge
      // Description card + heading.
      expect(find.text('Description'), findsOneWidget);
      expect(find.text('Welcome address'), findsOneWidget);
      // Speakers section + a speaker card.
      expect(find.text('Speakers'), findsOneWidget);
      expect(find.text('Dr Reef'), findsOneWidget);
      // Hall + category tag pills.
      expect(find.text('Main Hall'), findsOneWidget);
      expect(find.text('Main Session'), findsOneWidget);
      // The two CTAs.
      expect(
        find.widgetWithText(FilledButton, 'Add to calendar'),
        findsOneWidget,
      );
      expect(find.widgetWithText(OutlinedButton, 'Reminder'), findsOneWidget);
    });

    testWidgets('renders the seat card with the gold marker and CTAs together',
        (tester) async {
      // Tall surface so the whole lazy ListView (down to the CTA row) lays out.
      tester.view.physicalSize = const Size(1200, 2600);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail(), seat: _seat),
        controller: _SignedInController(),
      );

      // The whole KSA structure renders in one pass: header card, description,
      // speakers, the my-seat card and the CTA row — no overflow / exception.
      expect(find.text('Session detail'), findsOneWidget);
      expect(find.text('My seat'), findsOneWidget);
      expect(find.text('Row B · Seat 12'), findsOneWidget);
      expect(find.text('Show your badge at entry'), findsOneWidget);
      expect(
        find.widgetWithText(FilledButton, 'Add to calendar'),
        findsOneWidget,
      );
      expect(find.widgetWithText(OutlinedButton, 'Reminder'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('PAR-D2/D-extra — RTL: the gold CTA, the speaker role box and '
        'the seat marker lead at the inline start (right); the chevron trails '
        '(left)', (tester) async {
      tester.view.physicalSize = const Size(1200, 2600);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail(), seat: _seat),
        controller: _SignedInController(),
        locale: const Locale('ar'),
      );

      // Speaker role box (anchor) sits to the right of the speaker name.
      final anchorDx = tester.getCenter(find.byIcon(Icons.anchor)).dx;
      final nameDx = tester.getCenter(find.text('د. ريف')).dx;
      expect(anchorDx, greaterThan(nameDx));

      // Gold add-to-calendar (FilledButton) sits to the right of the reminder.
      final filledDx = tester.getCenter(find.byType(FilledButton)).dx;
      final outlinedDx = tester.getCenter(find.byType(OutlinedButton)).dx;
      expect(filledDx, greaterThan(outlinedDx));

      // The seat-card chevron sits at the inline end (far left).
      final chevronDx = tester.getCenter(find.byIcon(Icons.chevron_left)).dx;
      expect(chevronDx, lessThan(anchorDx));
    });

    testWidgets('a guest sees no my-seat card', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail(), seat: _seat),
        controller: _GuestController(),
      );
      // The guest path never calls the seat endpoint → no card.
      expect(find.text('My seat'), findsNothing);
      expect(find.textContaining('Seat 12'), findsNothing);
    });

    testWidgets('a signed-in account with a reservation sees the seat card',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail(), seat: _seat),
        controller: _SignedInController(),
      );

      expect(find.text('My seat'), findsOneWidget);
      expect(find.text('Row B · Seat 12'), findsOneWidget);
    });

    testWidgets('a signed-in account with no reservation sees no card',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        controller: _SignedInController(),
      );
      expect(find.text('My seat'), findsNothing);
    });

    testWidgets('tapping a speaker navigates to the speaker profile',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        controller: _GuestController(),
      );

      await tester.tap(find.text('Dr Reef'));
      await tester.pumpAndSettle();
      expect(find.text('SPEAKER sp1'), findsOneWidget);
    });

    testWidgets('Add-to-calendar shows the success toast', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        controller: _GuestController(),
        calendar: _FakeCalendar(),
      );

      await tester.tap(find.widgetWithText(FilledButton, 'Add to calendar'));
      await tester.pumpAndSettle();
      expect(find.text('Added to your calendar'), findsOneWidget);
    });

    testWidgets('Reminder shows the deferred-notice toast', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        controller: _GuestController(),
      );

      await tester.tap(find.widgetWithText(OutlinedButton, 'Reminder'));
      await tester.pumpAndSettle();
      final reminderToast = find.text('Reminders arrive with notifications setup.');
      expect(reminderToast, findsOneWidget);
    });

    testWidgets('a 404 shows the not-found state', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detailStatus: 404),
        controller: _GuestController(),
      );
      expect(find.text('This session was not found'), findsOneWidget);
    });

    testWidgets('a non-404 failure shows error + retry, which re-fetches',
        (tester) async {
      final repo = _FakeDetailRepo(detailStatus: 500);
      await _pump(tester, repo: repo, controller: _GuestController());

      expect(find.text('Could not load the session.'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();
      expect(repo.detailCalls, greaterThanOrEqualTo(2));
    });
  });
}
