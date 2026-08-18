import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/my_seat_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

SessionSeatMap _map() => const SessionSeatMap(
      rowLabels: <String>['A', 'B'],
      seatsPerRow: 3,
      reservedCells: <SeatCell>[
        SeatCell(
            rowLabel: 'A',
            seatNumber: 1,
            kind: SeatReservationKind.userBooking,),
      ],
      myCell: SeatCell(
        rowLabel: 'B',
        seatNumber: 2,
        kind: SeatReservationKind.userBooking,
      ),
      activeReservedCount: 2,
      hallCapacity: 6,
    );

class _FakeSeatRepo implements SeatMapRepository {
  _FakeSeatRepo({this.map, this.status});

  final SessionSeatMap? map;
  final int? status;
  int calls = 0;

  @override
  Future<SessionSeatMap> getSeatMap(String sessionId) async {
    calls++;
    if (status != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return map!;
  }

  // D-485 — the My-Seat screen is read-only; the write methods are unused here.
  @override
  Future<MyReservation> reserveSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) =>
      throw UnimplementedError();

  @override
  Future<MyReservation> reserveRandom(String sessionId) =>
      throw UnimplementedError();

  @override
  Future<MyReservation> joinOpenSeating(String sessionId) =>
      throw UnimplementedError();

  @override
  Future<void> releaseMine(String sessionId) => throw UnimplementedError();

  // B1 — the change-seat action opens the picker, which owns the move call; the
  // My-Seat screen itself never calls it.
  @override
  Future<MyReservation> moveSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) =>
      throw UnimplementedError();
}

/// Flattens an [InlineSpan] tree to its non-blank text leaves (Text.rich wraps
/// the supplied span under a default-style parent, so the real leaves are
/// nested).
List<TextSpan> _textLeaves(InlineSpan span) {
  final leaves = <TextSpan>[];
  if (span is TextSpan) {
    if ((span.text ?? '').trim().isNotEmpty) {
      leaves.add(span);
    }
    for (final child in span.children ?? const <InlineSpan>[]) {
      leaves.addAll(_textLeaves(child));
    }
  }
  return leaves;
}

class _FakeSeatShare implements SeatShare {
  String? shared;

  @override
  Future<void> shareText(String text) async {
    shared = text;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required SeatMapRepository repo,
  SeatShare? share,
  Locale locale = const Locale('en'),
  // The page's ListView builds lazily, so anything under the hall card is not
  // in the tree on the default 800x600 surface. A test that asserts on the
  // action row asks for a surface tall enough to build it.
  Size? surface,
}) async {
  if (surface != null) {
    tester.view.physicalSize = surface;
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
  }
  final router = GoRouter(
    initialLocation: '/sessions/s1/my-seat',
    routes: <RouteBase>[
      GoRoute(
        path: '/sessions/:sessionId/my-seat',
        name: RouteNames.mySeat,
        builder: (_, state) => MySeatScreen(
          sessionId: state.pathParameters['sessionId'] ?? '',
        ),
      ),
      GoRoute(
        path: '/map',
        name: RouteNames.venueMap,
        builder: (_, __) => const Scaffold(body: Text('MAP')),
      ),
      // B1 — the change-seat action pushes the real seat-picker route; the stub
      // stands in for it and pops `true` (a successful move) so the My-Seat
      // screen's re-read can be asserted.
      GoRoute(
        path: '/sessions/:sessionId/pick-seat',
        name: RouteNames.seatPicker,
        builder: (context, __) => Scaffold(
          body: Center(
            child: ElevatedButton(
              onPressed: () => context.pop(true),
              child: const Text('MOVED'),
            ),
          ),
        ),
      ),
    ],
  );

  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        seatMapRepositoryProvider.overrideWithValue(repo),
        seatShareProvider.overrideWithValue(share ?? _FakeSeatShare()),
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

/// The action row is the last child of a lazy [ListView], and the hall grid is
/// tall enough that it is never built at the default surface size — so it has
/// to be scrolled into view before it can be tapped. `.first` is the screen's
/// own ListView; the hall card nests two more scrollables below it.
Future<void> _tapAction(WidgetTester tester, Finder action) async {
  await tester.scrollUntilVisible(
    action,
    300,
    scrollable: find.byType(Scrollable).first,
  );
  await tester.tap(action);
  await tester.pumpAndSettle();
}

void main() {
  group('MySeatScreen (Page 018)', () {
    testWidgets('renders the session card + grid + legend', (tester) async {
      await _pump(tester, repo: _FakeSeatRepo(map: _map()));

      expect(find.text('Session'), findsOneWidget); // session-card label
      expect(find.text('Row B · Seat 2'), findsOneWidget); // myCell title
      expect(find.text('Your seat'), findsOneWidget); // legend
      expect(find.text('Available'), findsOneWidget);
      expect(find.text('Reserved'), findsOneWidget);
      expect(find.text('Stage · STAGE'), findsOneWidget); // gold stage band
    });

    testWidgets('both chip values render white; only the label word is gold',
        (tester) async {
      // Frame 905:1577/1579 regression guard: each chip is a Text.rich with a
      // [goldLabel, ' ', value] span; the value (12 / B) must be white
      // (surface), the leading label word (مقعد / الصف) gold (accent). The two
      // chips are the only rich texts on the page with two non-blank leaves.
      await _pump(tester, repo: _FakeSeatRepo(map: _map()));

      final chips = tester
          .widgetList<RichText>(find.byType(RichText))
          .map((rt) => _textLeaves(rt.text))
          .where((leaves) => leaves.length == 2)
          .toList();
      expect(chips.length, 2); // the seat + row chips
      for (final leaves in chips) {
        expect(leaves.first.style?.color, SimfTokens.accent); // label word
        expect(leaves.last.style?.color, SimfTokens.surface); // value
      }
    });

    // Both of these assert on the ACTION ROW at the bottom of the page, so both
    // need the tall surface `_pump` documents — the page's ListView builds
    // lazily and the row is not in the tree on the default 800x600. They passed
    // without it only while the page was short enough for the row to fall
    // inside the default viewport; the content added above it since (D-767
    // per-row seat counts, then the B1 "Change seat" CTA) pushed it out, and
    // the finders started returning 0 widgets. The buttons themselves are
    // unchanged — this is the test catching up with the page's height, not a
    // screen regression.
    testWidgets('share sends the seat-location text', (tester) async {
      final share = _FakeSeatShare();
      await _pump(
        tester,
        repo: _FakeSeatRepo(map: _map()),
        share: share,
        surface: const Size(1000, 2600),
      );

      await _tapAction(
        tester,
        find.widgetWithText(OutlinedButton, 'Share location'),
      );
      expect(share.shared, 'My SIMF seat: Row B · Seat 2');
    });

    testWidgets('navigate opens the venue map', (tester) async {
      await _pump(
        tester,
        repo: _FakeSeatRepo(map: _map()),
        surface: const Size(1000, 2600),
      );

      await _tapAction(
        tester,
        find.widgetWithText(FilledButton, 'Guide me to my seat'),
      );
      expect(find.text('MAP'), findsOneWidget);
    });

    testWidgets(
        'B1 — the change-seat action opens the picker and re-reads the '
        'grid when the move lands', (tester) async {
      final repo = _FakeSeatRepo(map: _map());
      await _pump(tester, repo: repo, surface: const Size(1000, 2600));

      final before = repo.calls;
      final cta = find.widgetWithText(OutlinedButton, 'Change seat');
      expect(cta, findsOneWidget);
      await tester.tap(cta);
      await tester.pumpAndSettle();
      // The picker route is now on top; popping it with `true` (a successful
      // move) must invalidate the seat map so the new seat is shown.
      expect(find.text('MOVED'), findsOneWidget);
      await tester.tap(find.text('MOVED'));
      await tester.pumpAndSettle();
      expect(repo.calls, greaterThan(before));
    });

    testWidgets('B1 — an open-seating join offers no change-seat action',
        (tester) async {
      // General admission has no seat to move, so the CTA must not appear.
      const openSeating = SessionSeatMap(
        rowLabels: <String>['A'],
        seatsPerRow: 3,
        reservedCells: <SeatCell>[],
        myCell: SeatCell(
          rowLabel: '',
          seatNumber: 0,
          kind: SeatReservationKind.openSeating,
        ),
        activeReservedCount: 1,
        hallCapacity: 3,
      );
      await _pump(
        tester,
        repo: _FakeSeatRepo(map: openSeating),
        surface: const Size(1000, 2600),
      );

      // The surface is tall enough to build the whole page, so this absence is
      // real and not an artefact of lazy list building.
      expect(find.widgetWithText(OutlinedButton, 'Share location'),
          findsOneWidget,);
      expect(find.widgetWithText(OutlinedButton, 'Change seat'), findsNothing);
    });

    testWidgets('renders a ragged variable-width grid (per-row counts)',
        (tester) async {
      const ragged = SessionSeatMap(
        rowLabels: <String>['A', 'B'],
        seatsPerRow: 10, // legacy fallback = max(counts)
        seatCounts: <int>[4, 10],
        reservedCells: <SeatCell>[],
        activeReservedCount: 0,
        hallCapacity: 14,
        sessionTitle: 'Ragged hall',
      );
      await _pump(tester, repo: _FakeSeatRepo(map: ragged));

      expect(find.text('Ragged hall'), findsOneWidget);
      // Row B draws all 10 seats; seat number 10 is unique to it (row A has 4),
      // so its numeral renders exactly once — proof of the per-row count.
      expect(find.text('10'), findsOneWidget);
      expect(
          find.text('Your seat'), findsOneWidget,); // the legend still renders
    });

    testWidgets('an unconfigured hall shows the unavailable state',
        (tester) async {
      final empty = SessionSeatMap.fromJson(const <String, dynamic>{
        'rowLabels': <dynamic>[],
        'seatsPerRow': 0,
        'reservedCells': <dynamic>[],
        'activeReservedCount': 0,
        'hallCapacity': 0,
      });
      await _pump(tester, repo: _FakeSeatRepo(map: empty));
      expect(find.text('Seat map not available yet'), findsOneWidget);
    });

    testWidgets('a 404 shows the not-found state', (tester) async {
      await _pump(tester, repo: _FakeSeatRepo(status: 404));
      expect(find.text('This session was not found'), findsOneWidget);
    });

    testWidgets('an error shows retry, which re-fetches', (tester) async {
      final repo = _FakeSeatRepo(status: 500);
      await _pump(tester, repo: repo);

      expect(find.text('Could not load the seat map.'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();
      expect(repo.calls, greaterThanOrEqualTo(2));
    });
  });
}
