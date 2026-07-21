import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/data/session_calendar.dart';
import 'package:simf_app/features/sessions/data/session_detail_repository.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/session_detail_screen.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../accessibility/_fake_prefs.dart';

SessionDetail _detail({
  String? liveStreamUrl,
  int? countryId,
  DateTime? startUtc,
  DateTime? endUtc,
}) =>
    SessionDetail(
      id: 's1',
      code: 'OP-1',
      displayOrder: 2, // D-567 — gold badge shows "02"
      title: 'Opening',
      titleArabic: 'الافتتاح',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      // Default: an upcoming session (future) → the D-714 pre-session ask label.
      startUtc: startUtc ?? DateTime.utc(2026, 11, 23, 6),
      endUtc: endUtc ?? DateTime.utc(2026, 11, 23, 7),
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

// A session whose end time is in the past — the trigger for the one-time
// after-view rate prompt.
SessionDetail _endedDetail() => SessionDetail(
      id: 's1',
      code: 'OP-1',
      displayOrder: 2,
      title: 'Opening',
      titleArabic: 'الافتتاح',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      startUtc: DateTime.utc(2020, 1, 1, 6),
      endUtc: DateTime.utc(2020, 1, 1, 7),
      speakers: const <SessionSpeaker>[],
      description: 'Welcome address',
    );

// The avatar builds the photo URL from the base; the must-override data config
// provider throws otherwise. The test HTTP client fails the load → placeholder.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

// D-485 — the caller's own seat cell (an assigned-seat booking, still pending).
const _mySeatCell = SeatCell(
  reservationId: 'r1',
  rowLabel: 'B',
  seatNumber: 12,
  kind: SeatReservationKind.userBooking,
);

// D-572 — the same seat once the Control Panel has APPROVED the booking.
const _myApprovedSeatCell = SeatCell(
  reservationId: 'r1',
  rowLabel: 'B',
  seatNumber: 12,
  kind: SeatReservationKind.userBooking,
  status: BookingStatus.approved,
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

// A router with a home beneath the session detail (so a pop disposes the
// screen) plus a /rate landing that echoes its query so the after-view prompt
// is observable. Returns the router so the test can push / pop programmatically.
Future<GoRouter> _pumpRatePrompt(
  WidgetTester tester, {
  required SessionDetailRepository repo,
  required AuthController controller,
  required SimfPrefsStorage prefs,
}) async {
  tester.view.physicalSize = const Size(1200, 2600);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  final router = GoRouter(
    initialLocation: '/',
    routes: <RouteBase>[
      GoRoute(
        path: '/',
        name: 'home',
        builder: (_, __) => const Scaffold(body: Text('HOME')),
      ),
      GoRoute(
        path: '/sessions/:sessionId',
        name: RouteNames.sessionDetail,
        builder: (_, state) => SessionDetailScreen(
          sessionId: state.pathParameters['sessionId'] ?? '',
        ),
      ),
      GoRoute(
        path: '/rate',
        name: RouteNames.rate,
        builder: (_, state) => Scaffold(
          body: Text('RATE ${state.uri.queryParameters['targetId']} '
              '${state.uri.queryParameters['code']}'),
        ),
      ),
      GoRoute(
        path: '/speakers/:speakerId',
        name: RouteNames.speakerProfile,
        builder: (_, __) => const Scaffold(body: Text('SPEAKER')),
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
        simfPrefsStorageProvider.overrideWithValue(prefs),
        sessionDetailRepositoryProvider.overrideWithValue(repo),
        seatMapRepositoryProvider.overrideWithValue(_FakeSeatRepo(map: null)),
        sessionCalendarProvider.overrideWithValue(_FakeCalendar()),
        authControllerProvider.overrideWith(() => controller),
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
  return router;
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
      // The gold index badge shows the day-ordinal "02" (D-567), not the code.
      expect(find.text('02'), findsOneWidget);
      expect(find.text('OP-1'), findsNothing);
      // Header action buttons: both slots always render (Figma 889:2715); owner
      // 2026-07-14 gates them on state, so on this UPCOMING session both are
      // present but inactive (greyed) — see the gating tests below + in
      // session_detail_body_test.dart.
      expect(find.text('Session summary'), findsOneWidget);
      expect(find.text('Session link'), findsOneWidget);
      // Description card + heading.
      expect(find.text('Description'), findsOneWidget);
      expect(find.text('Welcome address'), findsOneWidget);
      // Speakers section + a speaker card.
      expect(find.text('Speakers'), findsOneWidget);
      expect(find.text('Dr Reef'), findsOneWidget);
      // The ask-the-host card (shown to everyone — Figma 1056:12876). This is an
      // upcoming session, so D-714 (GAP-2) shows the pre-session ask label.
      expect(find.text('Ask a question before it starts'), findsOneWidget);
      // The two CTAs.
      expect(
        find.widgetWithText(FilledButton, 'Add to calendar'),
        findsOneWidget,
      );
      expect(find.widgetWithText(OutlinedButton, 'Reminder'), findsOneWidget);
    });

    testWidgets('the session link opens the live screen while the session is '
        'live + streaming (owner 2026-07-14 gate)', (tester) async {
      final live = _detail(
        liveStreamUrl: 'https://youtu.be/abcdefghijk',
        startUtc: DateTime.now().toUtc().subtract(const Duration(hours: 1)),
        endUtc: DateTime.now().toUtc().add(const Duration(hours: 1)),
      );
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: live),
        controller: _GuestController(),
      );

      expect(find.text('Session link'), findsOneWidget);
      await tester.tap(find.text('Session link'));
      await tester.pumpAndSettle();
      expect(find.text('LIVE'), findsOneWidget);
    });

    testWidgets('the summary button opens the AI session summary once the '
        'session has ended (owner 2026-07-14 gate)', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _endedDetail()),
        controller: _GuestController(),
      );

      await tester.tap(find.text('Session summary'));
      await tester.pumpAndSettle();
      expect(find.text('AI-SUMMARY'), findsOneWidget);
    });

    testWidgets('#3 — a joined user can ask: the ask card opens '
        'send-question', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatMap: _seatMap(myCell: _mySeatCell), // joined → ask enabled
        controller: _SignedInController(),
      );

      // Upcoming session → the pre-session ask label (D-714 GAP-2).
      await tester.tap(find.text('Ask a question before it starts'));
      await tester.pumpAndSettle();
      expect(find.text('SEND-Q'), findsOneWidget);
    });

    testWidgets('#7 — an approved user can ask a FUTURE session without a '
        'booking (no join gate): the ask card is enabled and opens send-question',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatMap: _seatMap(), // approved, no reservation (myCell null)
        controller: _SignedInController(),
      );

      // #7 dropped the "join first" gate for the pre-start ask — no hint now.
      expect(find.text('Ask a question before it starts'), findsOneWidget);
      expect(find.text('Join the session to ask a question'), findsNothing);
      await tester.tap(find.text('Ask a question before it starts'));
      await tester.pumpAndSettle();
      expect(find.text('SEND-Q'), findsOneWidget);
    });

    testWidgets('S-4 — an in-window in-person session (no live URL) SHOWS the '
        'ask card with the neutral live label on the detail', (tester) async {
      final ongoing = _detail(
        startUtc: DateTime.now().toUtc().subtract(const Duration(hours: 1)),
        endUtc: DateTime.now().toUtc().add(const Duration(hours: 1)),
      );
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: ongoing),
        seatMap: _seatMap(myCell: _mySeatCell),
        controller: _SignedInController(),
      );

      // Live + in-person (no broadcast feed) → the in-hall ask, using the
      // neutral label; the pre-session label is future-only.
      expect(find.text('Ask a question'), findsOneWidget);
      expect(find.text('Ask a question before it starts'), findsNothing);
    });

    testWidgets('S-4 — an in-window BROADCAST session (live URL) HIDES the ask '
        'card on the detail (asking moves to the live-broadcast screen)',
        (tester) async {
      final broadcasting = _detail(
        startUtc: DateTime.now().toUtc().subtract(const Duration(hours: 1)),
        endUtc: DateTime.now().toUtc().add(const Duration(hours: 1)),
        liveStreamUrl: 'https://live.example.sa/main.m3u8',
      );
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: broadcasting),
        seatMap: _seatMap(myCell: _mySeatCell),
        controller: _SignedInController(),
      );

      // A broadcast session's live ask lives on the live-broadcast screen, so
      // the detail must not double it — no ask card here.
      expect(find.text('Ask a question'), findsNothing);
      expect(find.text('Ask a question before it starts'), findsNothing);
    });

    testWidgets('#7 — a PAST (ended) session HIDES the ask card (the after-view '
        'is a recording, not a live broadcast)', (tester) async {
      // After EndUtc `_showAsk` returns false regardless of the viewer, so a
      // guest proves it — and a guest never trips the approved-attendee
      // after-view rate prompt.
      final ended = _detail(
        startUtc: DateTime.now().toUtc().subtract(const Duration(hours: 3)),
        endUtc: DateTime.now().toUtc().subtract(const Duration(hours: 2)),
      );
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: ended),
        controller: _GuestController(),
      );

      expect(find.text('Ask a question before it starts'), findsNothing);
      expect(find.text('Ask the host'), findsNothing);
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
      // Owner 2026-06-30 — cancel is now a plain white "Cancel" line under the
      // CTA row (was the red "Cancel booking" link inside the card).
      expect(find.text('Cancel'), findsOneWidget);
      expect(
        find.widgetWithText(FilledButton, 'Add to calendar'),
        findsOneWidget,
      );
      expect(find.widgetWithText(OutlinedButton, 'Reminder'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('D-572 — an APPROVED booking shows the show-badge hint, not the '
        'pending line', (tester) async {
      await _pump(
        tester,
        repo: _FakeDetailRepo(detail: _detail()),
        seatMap: _seatMap(myCell: _myApprovedSeatCell),
        controller: _SignedInController(),
      );

      expect(find.text('Row B · Seat 12'), findsOneWidget);
      expect(find.text('Show your badge at entry'), findsOneWidget);
      expect(find.text('Pending approval'), findsNothing);
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

      // Open the confirm dialog from the white "Cancel" link under the CTA row.
      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
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

      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
      await tester.pumpAndSettle();
      // Tap the dialog's dismiss (Cancel) button — scoped to the dialog so it
      // doesn't collide with the screen's own white "Cancel" link.
      await tester.tap(
        find.descendant(
          of: find.byType(Dialog),
          matching: find.widgetWithText(TextButton, 'Cancel'),
        ),
      );
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

      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
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

      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
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

      // The reservation-card chevron (the thin stroked left chevron,
      // ic_back.svg per Figma 889:2762 — not the filled triangle) sits at the
      // inline end (far left). The header back button uses the same asset at
      // size 24; the seat chevron is the size-20 one.
      final chevronDx = tester
          .getCenter(
            find.byWidgetPredicate(
              (w) =>
                  w is SimfSvgIcon &&
                  w.asset == 'assets/icons/ic_back.svg' &&
                  w.size == 20,
            ),
          )
          .dx;
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
      expect(find.text('Join the session'), findsNothing);
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
      // Owner 2026-06-30 — no section heading now; one gold join button, unified
      // label for both modes. Assigned-seat still opens the picker.
      final cta = find.widgetWithText(FilledButton, 'Join the session');
      expect(cta, findsOneWidget);
      await tester.tap(cta);
      await tester.pumpAndSettle();
      expect(find.text('PICKER'), findsOneWidget);
    });

    testWidgets('D-750 — signed-in, open-seating, no reservation → the CTA reads '
        '"register to attend", Join confirms then shows the success alert',
        (tester) async {
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

      // D-750 — the open-seating CTA reads "register to attend", not the
      // generic join label (the label the assigned-seat mode keeps).
      final cta =
          find.widgetWithText(FilledButton, 'Register to attend the session');
      expect(cta, findsOneWidget);
      expect(
        find.widgetWithText(FilledButton, 'Join the session'),
        findsNothing,
      );
      await tester.tap(cta);
      await tester.pumpAndSettle();
      // The confirm dialog, then Join.
      expect(find.text('Join this session?'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Join'));
      await tester.pumpAndSettle();
      expect(seatRepoHolder.joinCalls, 1);
      // D-750 — a one-button success alert (not the old pending-toast snackbar)
      // explaining that registering is not a seat reservation.
      expect(
        find.textContaining('You have successfully registered to attend'),
        findsOneWidget,
      );
      expect(find.text('Request sent — pending approval'), findsNothing);
      // The single OK button dismisses it.
      await tester.tap(find.widgetWithText(FilledButton, 'OK'));
      await tester.pumpAndSettle();
      expect(
        find.textContaining('You have successfully registered to attend'),
        findsNothing,
      );
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

  group('after-view rate prompt (one-time per session)', () {
    testWidgets('an approved attendee leaving an ENDED session is prompted to '
        'rate it exactly once', (tester) async {
      final prefs = FakePrefs();
      final router = await _pumpRatePrompt(
        tester,
        repo: _FakeDetailRepo(detail: _endedDetail()),
        controller: _SignedInController(),
        prefs: prefs,
      );

      // First view of the ended session.
      router.push('/sessions/s1');
      await tester.pumpAndSettle();
      expect(find.text('Session detail'), findsOneWidget);

      // Leaving it opens the dynamic rate screen for THIS session.
      router.pop();
      await tester.pumpAndSettle();
      expect(find.text('RATE s1 Session'), findsOneWidget);

      // Leave the rate screen.
      router.pop();
      await tester.pumpAndSettle();
      expect(find.text('HOME'), findsOneWidget);

      // Second view of the same ended session → it must NOT prompt again.
      router.push('/sessions/s1');
      await tester.pumpAndSettle();
      router.pop();
      await tester.pumpAndSettle();
      expect(find.text('RATE s1 Session'), findsNothing);
      expect(find.text('HOME'), findsOneWidget);
    });

    testWidgets('a guest leaving an ended session is NOT prompted',
        (tester) async {
      final router = await _pumpRatePrompt(
        tester,
        repo: _FakeDetailRepo(detail: _endedDetail()),
        controller: _GuestController(),
        prefs: FakePrefs(),
      );

      router.push('/sessions/s1');
      await tester.pumpAndSettle();
      router.pop();
      await tester.pumpAndSettle();
      expect(find.text('RATE s1 Session'), findsNothing);
      expect(find.text('HOME'), findsOneWidget);
    });

    testWidgets('an approved attendee leaving an UPCOMING (not-yet-ended) '
        'session is NOT prompted', (tester) async {
      final router = await _pumpRatePrompt(
        tester,
        repo: _FakeDetailRepo(detail: _detail()), // future endUtc
        controller: _SignedInController(),
        prefs: FakePrefs(),
      );

      router.push('/sessions/s1');
      await tester.pumpAndSettle();
      router.pop();
      await tester.pumpAndSettle();
      expect(find.text('RATE s1 Session'), findsNothing);
      expect(find.text('HOME'), findsOneWidget);
    });
  });
}
