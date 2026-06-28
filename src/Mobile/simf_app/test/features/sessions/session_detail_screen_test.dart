import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/data/session_calendar.dart';
import 'package:simf_app/features/sessions/data/session_detail_repository.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/session_detail_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

SessionDetail _detail({String? liveStreamUrl, int? countryId}) => SessionDetail(
      id: 's1',
      code: 'OP-1',
      title: 'Opening',
      titleArabic: 'الافتتاح',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      startUtc: DateTime.utc(2026, 11, 23, 6),
      endUtc: DateTime.utc(2026, 11, 23, 7),
      speakers: <SessionSpeaker>[
        SessionSpeaker(
          id: 'sp1',
          name: 'Dr Reef',
          nameArabic: 'د. ريف',
          displayOrder: 0,
          role: SessionSpeakerRole.speaker,
          title: 'Chief Scientist',
          countryNameEn: 'Saudi Arabia',
          countryId: countryId,
        ),
      ],
      description: 'Welcome address',
      categoryName: 'Main Session',
      categoryNameArabic: 'جلسة رئيسية',
      liveStreamUrl: liveStreamUrl,
    );

// The avatar builds the photo URL from the base; the must-override data config
// provider throws otherwise. The test HTTP client fails the load → placeholder.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

// D-485 — the caller's own seat cell (an assigned-seat booking).
const _mySeatCell = SeatCell(
  reservationId: 'r1',
  rowLabel: 'B',
  seatNumber: 12,
  kind: SeatReservationKind.userBooking,
);

SessionSeatMap _seatMap({
  SeatCell? myCell,
  SeatSelectionMode mode = SeatSelectionMode.assignedSeat,
}) =>
    SessionSeatMap(
      rowLabels: const <String>['A', 'B'],
      seatsPerRow: 12,
      reservedCells: const <SeatCell>[],
      activeReservedCount: 0,
      hallCapacity: 24,
      myCell: myCell,
      mode: mode,
    );

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
  _FakeDetailRepo({this.detail, this.detailStatus});

  final SessionDetail? detail;
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

  // D-485 — the screen now reads the seat MAP (mode + myCell) via
  // SeatMapRepository; getMySeat is unused but part of the interface.
  @override
  Future<MySeat?> getMySeat(String sessionId) async => null;
}

class _FakeSeatRepo implements SeatMapRepository {
  _FakeSeatRepo({this.map, this.releaseFailure});

  /// Null → getSeatMap throws (e.g. a pending 403) so the join section hides.
  final SessionSeatMap? map;

  /// When set, [releaseMine] throws it — drives the cancel-reservation
  /// failure path (the D-485/this-session fix that surfaces the backend reason).
  final ApiFailure? releaseFailure;
  int joinCalls = 0;
  int reserveCalls = 0;
  int releaseCalls = 0;

  @override
  Future<SessionSeatMap> getSeatMap(String sessionId) async {
    final m = map;
    if (m == null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: 403,
      );
    }
    return m;
  }

  @override
  Future<MyReservation> joinOpenSeating(String sessionId) async {
    joinCalls++;
    return const MyReservation(
      reservationId: 'r9',
      sessionId: 's1',
      kind: SeatReservationKind.openSeating,
      status: BookingStatus.pending,
    );
  }

  @override
  Future<MyReservation> reserveSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) async {
    reserveCalls++;
    return MyReservation(
      reservationId: 'r9',
      sessionId: 's1',
      rowLabel: rowLabel,
      seatNumber: seatNumber,
      kind: SeatReservationKind.userBooking,
      status: BookingStatus.pending,
    );
  }

  @override
  Future<MyReservation> reserveRandom(String sessionId) =>
      throw UnimplementedError();

  @override
  Future<void> releaseMine(String sessionId) async {
    releaseCalls++;
    final failure = releaseFailure;
    if (failure != null) {
      throw failure;
    }
  }
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
  SessionSeatMap? seatMap,
  SeatMapRepository? seatRepo,
  SessionCalendar? calendar,
  Locale locale = const Locale('en'),
}) async {
  // A tall surface so the whole lazy ListView (down to the CTA row) lays out —
  // the restructured frame (header buttons + ask-host card) pushes the lower
  // sections past the default 800×600 viewport otherwise.
  tester.view.physicalSize = const Size(1200, 2600);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
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
      GoRoute(
        path: '/sessions/:sessionId/pick-seat',
        name: RouteNames.seatPicker,
        builder: (_, __) => const Scaffold(body: Text('PICKER')),
      ),
      GoRoute(
        path: '/live',
        name: RouteNames.liveBroadcast,
        builder: (_, __) => const Scaffold(body: Text('LIVE')),
      ),
      GoRoute(
        path: '/ai-summary',
        name: RouteNames.aiSummary,
        builder: (_, __) => const Scaffold(body: Text('AI-SUMMARY')),
      ),
      GoRoute(
        path: '/live/question',
        name: RouteNames.sendQuestion,
        builder: (_, __) => const Scaffold(body: Text('SEND-Q')),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        simfDataConfigProvider.overrideWithValue(_testConfig),
        sessionDetailRepositoryProvider.overrideWithValue(repo),
        seatMapRepositoryProvider
            .overrideWithValue(seatRepo ?? _FakeSeatRepo(map: seatMap)),
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
    testWidgets('renders the KSA detail (header card, summary button, '
        'description, speaker, ask-host, CTAs)', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        controller: _GuestController(),
      );

      // Header card: the centred title chrome + the title/code + meta.
      expect(find.text('Session detail'), findsOneWidget);
      expect(find.text('Opening'), findsOneWidget);
      expect(find.text('OP-1'), findsOneWidget); // the gold index badge
      // Header action buttons: the summary button always shows; the live link
      // is hidden because this detail has no liveStreamUrl (Figma 889:2715).
      expect(find.text('Session summary'), findsOneWidget);
      expect(find.text('Session link'), findsNothing);
      // Description card + heading.
      expect(find.text('Description'), findsOneWidget);
      expect(find.text('Welcome address'), findsOneWidget);
      // Speakers section + a speaker card.
      expect(find.text('Speakers'), findsOneWidget);
      expect(find.text('Dr Reef'), findsOneWidget);
      // The ask-the-host card (shown to everyone — Figma 1056:12876).
      expect(find.text('Ask the host'), findsOneWidget);
      // The two CTAs.
      expect(
        find.widgetWithText(FilledButton, 'Add to calendar'),
        findsOneWidget,
      );
      expect(find.widgetWithText(OutlinedButton, 'Reminder'), findsOneWidget);
    });

    testWidgets('the live link shows only when the session has a feed, and '
        'opens the live screen', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(
          detail: _detail(liveStreamUrl: 'https://youtu.be/abcdefghijk'),
        ),
        controller: _GuestController(),
      );

      expect(find.text('Session link'), findsOneWidget);
      await tester.tap(find.text('Session link'));
      await tester.pumpAndSettle();
      expect(find.text('LIVE'), findsOneWidget);
    });

    testWidgets('the summary button opens the AI session summary',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        controller: _GuestController(),
      );

      await tester.tap(find.text('Session summary'));
      await tester.pumpAndSettle();
      expect(find.text('AI-SUMMARY'), findsOneWidget);
    });

    testWidgets('#3 — a joined user can ask: the ask-host card opens '
        'send-question', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatMap: _seatMap(myCell: _mySeatCell), // joined → ask enabled
        controller: _SignedInController(),
      );

      await tester.tap(find.text('Ask the host'));
      await tester.pumpAndSettle();
      expect(find.text('SEND-Q'), findsOneWidget);
    });

    testWidgets('#3 — pre-ask is gated on joining: not joined → the ask card is '
        'disabled with a hint and does not open send-question', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatMap: _seatMap(), // approved, no reservation → not joined
        controller: _SignedInController(),
      );

      expect(find.text('Ask the host'), findsOneWidget);
      expect(find.text('Join the session to ask a question'), findsOneWidget);
      await tester.tap(find.text('Ask the host'));
      await tester.pumpAndSettle();
      expect(find.text('SEND-Q'), findsNothing); // tap is inert until joined
    });

    testWidgets('a speaker with a country code renders its flag emoji',
        (tester) async {
      // 682 = Saudi Arabia → 🇸🇦 (U+1F1F8 U+1F1E6).
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail(countryId: 682)),
        controller: _GuestController(),
      );

      expect(find.text('\u{1F1F8}\u{1F1E6}'), findsOneWidget);
    });

    testWidgets('a held reservation shows the booking card (seat + pending '
        '+ cancel) with the CTAs', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatMap: _seatMap(myCell: _mySeatCell),
        controller: _SignedInController(),
      );

      expect(find.text('Session detail'), findsOneWidget);
      expect(find.text('My seat'), findsOneWidget);
      expect(find.text('Row B · Seat 12'), findsOneWidget);
      // D-485 — the pending-approval hint replaced the badge hint.
      expect(find.text('Pending approval'), findsOneWidget);
      expect(find.text('Cancel booking'), findsOneWidget);
      expect(
        find.widgetWithText(FilledButton, 'Add to calendar'),
        findsOneWidget,
      );
      expect(find.widgetWithText(OutlinedButton, 'Reminder'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('#1 — Cancel booking confirms then releases the seat and shows '
        'the success toast', (tester) async {
      final seatRepo = _FakeSeatRepo(map: _seatMap(myCell: _mySeatCell));
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatRepo: seatRepo,
        controller: _SignedInController(),
      );

      // Open the confirm dialog from the booking card's cancel button.
      await tester.tap(find.widgetWithText(TextButton, 'Cancel booking'));
      await tester.pumpAndSettle();
      expect(find.text('Cancel booking?'), findsOneWidget);

      // Confirm — the dialog's gold action releases the seat.
      await tester.tap(find.widgetWithText(FilledButton, 'Cancel booking'));
      await tester.pumpAndSettle();

      expect(seatRepo.releaseCalls, 1);
      expect(find.text('Booking cancelled'), findsOneWidget);
    });

    testWidgets('#1 — dismissing the cancel dialog does NOT release the seat',
        (tester) async {
      final seatRepo = _FakeSeatRepo(map: _seatMap(myCell: _mySeatCell));
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatRepo: seatRepo,
        controller: _SignedInController(),
      );

      await tester.tap(find.widgetWithText(TextButton, 'Cancel booking'));
      await tester.pumpAndSettle();
      // Tap the dialog's dismiss (Cancel) button.
      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
      await tester.pumpAndSettle();

      expect(seatRepo.releaseCalls, 0);
      expect(find.text('Booking cancelled'), findsNothing);
    });

    testWidgets('#1 fix — a cancel failure surfaces the backend reason '
        '(not the generic toast)', (tester) async {
      // The exact failure the owner saw: cancel "looks broken" because the
      // real 409 reason was swallowed behind a generic toast.
      final seatRepo = _FakeSeatRepo(
        map: _seatMap(myCell: _mySeatCell),
        releaseFailure: ApiFailure(
          code: ApiErrorCodes.clientNetwork,
          message: 'You cannot cancel after the session has started',
          httpStatus: 409,
        ),
      );
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatRepo: seatRepo,
        controller: _SignedInController(),
      );

      await tester.tap(find.widgetWithText(TextButton, 'Cancel booking'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Cancel booking'));
      await tester.pumpAndSettle();

      expect(seatRepo.releaseCalls, 1);
      // The real backend reason is shown verbatim …
      expect(
        find.text('You cannot cancel after the session has started'),
        findsOneWidget,
      );
      // … not the generic fallback.
      expect(find.text("Couldn't cancel the booking"), findsNothing);
    });

    testWidgets('#1 fix — a reason-less cancel failure falls back to the '
        'generic toast', (tester) async {
      final seatRepo = _FakeSeatRepo(
        map: _seatMap(myCell: _mySeatCell),
        releaseFailure: ApiFailure(
          code: ApiErrorCodes.clientNetwork,
          message: '   ', // whitespace-only → no usable reason
          httpStatus: 500,
        ),
      );
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatRepo: seatRepo,
        controller: _SignedInController(),
      );

      await tester.tap(find.widgetWithText(TextButton, 'Cancel booking'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Cancel booking'));
      await tester.pumpAndSettle();

      expect(find.text("Couldn't cancel the booking"), findsOneWidget);
    });

    testWidgets('PAR-D2/D-extra — RTL: the gold CTA and the speaker photo lead '
        'at the inline start (right); the seat chevron trails (left)',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatMap: _seatMap(myCell: _mySeatCell),
        controller: _SignedInController(),
        locale: const Locale('ar'),
      );

      // The speaker photo (the only Image) sits to the right of the name.
      final photoDx = tester.getCenter(find.byType(Image)).dx;
      final nameDx = tester.getCenter(find.text('د. ريف')).dx;
      expect(photoDx, greaterThan(nameDx));

      // Gold add-to-calendar (FilledButton) sits to the right of the reminder.
      final filledDx = tester
          .getCenter(find.widgetWithText(FilledButton, 'أضف إلى تقويمي'))
          .dx;
      final outlinedDx = tester.getCenter(find.byType(OutlinedButton)).dx;
      expect(filledDx, greaterThan(outlinedDx));

      // The reservation-card chevron sits at the inline end (far left).
      final chevronDx = tester.getCenter(find.byIcon(Icons.chevron_left)).dx;
      expect(chevronDx, lessThan(nameDx));
    });

    testWidgets('a guest sees no join section', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatMap: _seatMap(myCell: _mySeatCell),
        controller: _GuestController(),
      );
      // The guest path never calls the seat endpoint → no card, no Join CTA.
      expect(find.text('My seat'), findsNothing);
      expect(find.textContaining('Seat 12'), findsNothing);
      expect(find.text('Join this session'), findsNothing);
    });

    testWidgets('signed-in, assigned-seat, no reservation → the Select-my-seat '
        'CTA opens the seat picker', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatMap: _seatMap(mode: SeatSelectionMode.assignedSeat),
        controller: _SignedInController(),
      );

      expect(find.text('My seat'), findsNothing);
      expect(find.text('Join this session'), findsOneWidget); // section heading
      final cta = find.widgetWithText(FilledButton, 'Select my seat');
      expect(cta, findsOneWidget);
      await tester.tap(cta);
      await tester.pumpAndSettle();
      expect(find.text('PICKER'), findsOneWidget);
    });

    testWidgets('signed-in, open-seating, no reservation → Join confirms then '
        'joins (pending toast)', (tester) async {
      final seatRepoHolder = _FakeSeatRepo(
        map: _seatMap(mode: SeatSelectionMode.openSeating),
      );
      tester.view.physicalSize = const Size(1200, 2600);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(
        ProviderScope(
          overrides: <Override>[
            simfDataConfigProvider.overrideWithValue(_testConfig),
            sessionDetailRepositoryProvider
                .overrideWithValue(_FakeDetailRepo(detail: _detail())),
            seatMapRepositoryProvider.overrideWithValue(seatRepoHolder),
            sessionCalendarProvider.overrideWithValue(_FakeCalendar()),
            authControllerProvider.overrideWith(_SignedInController.new),
          ],
          child: MaterialApp(
            locale: const Locale('en'),
            supportedLocales: AppL10n.supportedLocales,
            localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
              ...AppL10n.localizationsDelegates,
              GlobalMaterialLocalizations.delegate,
              GlobalWidgetsLocalizations.delegate,
              GlobalCupertinoLocalizations.delegate,
            ],
            home: const SessionDetailScreen(sessionId: 's1'),
          ),
        ),
      );
      await tester.pumpAndSettle();

      final cta = find.widgetWithText(FilledButton, 'Join this session');
      expect(cta, findsOneWidget);
      await tester.tap(cta);
      await tester.pumpAndSettle();
      // The confirm dialog, then Join.
      expect(find.text('Join this session?'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Join'));
      await tester.pumpAndSettle();
      expect(seatRepoHolder.joinCalls, 1);
      expect(find.text('Request sent — pending approval'), findsOneWidget);
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
