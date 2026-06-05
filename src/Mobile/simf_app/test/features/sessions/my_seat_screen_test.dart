import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
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
    testWidgets('renders the banner + grid + legend', (tester) async {
      await _pump(tester, repo: _FakeSeatRepo(map: _map()));

      expect(find.text('Row B · Seat 2'), findsOneWidget); // banner (myCell)
      expect(find.text('2 of 6 reserved'), findsOneWidget);
      expect(find.text('Your seat'), findsOneWidget); // legend
      expect(find.text('Available'), findsOneWidget);
      expect(find.text('Reserved'), findsOneWidget);
      expect(find.text('Stage'), findsOneWidget);
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
