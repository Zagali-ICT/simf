import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/my_seat_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

SessionSeatMap _map() => const SessionSeatMap(
      rowLabels: <String>['A', 'B'],
      seatsPerRow: 3,
      reservedCells: <SeatCell>[
        SeatCell(rowLabel: 'A', seatNumber: 1, kind: SeatReservationKind.userBooking),
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
}

/// Flattens an [InlineSpan] tree to its non-blank text leaves (Text.rich wraps
/// the supplied span under a default-style parent, so the real leaves are nested).
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
}) async {
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
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
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

    testWidgets('share sends the seat-location text', (tester) async {
      final share = _FakeSeatShare();
      await _pump(tester, repo: _FakeSeatRepo(map: _map()), share: share);

      await tester.tap(find.widgetWithText(OutlinedButton, 'Share location'));
      await tester.pumpAndSettle();
      expect(share.shared, 'My SIMF seat: Row B · Seat 2');
    });

    testWidgets('navigate opens the venue map', (tester) async {
      await _pump(tester, repo: _FakeSeatRepo(map: _map()));

      await tester.tap(find.widgetWithText(FilledButton, 'Guide me to my seat'));
      await tester.pumpAndSettle();
      expect(find.text('MAP'), findsOneWidget);
    });

    testWidgets('an unconfigured hall shows the unavailable state',
        (tester) async {
      final empty = SessionSeatMap.fromJson(<String, dynamic>{
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
