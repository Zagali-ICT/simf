import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/ai_summary/session_summary_list_screen.dart';
import 'package:simf_app/features/ai_summary/session_summary_screen.dart'
    show aiSummarySessionsProvider;
import 'package:simf_app/features/myarea/data/my_sessions_models.dart';
import 'package:simf_app/features/myarea/data/my_sessions_repository.dart';
import 'package:simf_app/features/sessions/data/session_favourites.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';

/// A favourites controller seeded with a fixed set (no API).
class _FixedFavourites extends SessionFavouritesController {
  _FixedFavourites(this._ids);

  final Set<String> _ids;

  @override
  Future<Set<String>> build() async => _ids;
}

SessionListItem _item(String id, String title) => SessionListItem(
      id: id,
      code: 'C-$id',
      title: title,
      titleArabic: title,
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة',
      startUtc: DateTime.utc(2026, 11, 23, 6),
      endUtc: DateTime.utc(2026, 11, 23, 7),
      status: SessionStatus.scheduled,
      speakers: const <SessionSpeaker>[],
    );

MyAreaSessionItem _mine(String id) => MyAreaSessionItem(
      id: id,
      title: 't',
      titleArabic: 't',
      startUtc: DateTime.utc(2026, 11, 23, 6),
      endUtc: DateTime.utc(2026, 11, 23, 7),
      status: SessionStatus.scheduled,
      attended: false,
      isFavourite: false,
    );

Future<void> _pump(
  WidgetTester tester,
  List<SessionListItem> items, {
  Set<String> favourites = const <String>{},
  List<String> mine = const <String>[],
}) async {
  final router = GoRouter(
    initialLocation: '/session-summaries',
    routes: <RouteBase>[
      GoRoute(
        path: '/session-summaries',
        name: RouteNames.sessionSummaryList,
        builder: (_, __) => const SessionSummaryListScreen(),
      ),
      GoRoute(
        path: '/ai-summary',
        name: RouteNames.aiSummary,
        builder: (_, state) => Scaffold(
          body: Text('SUMMARY ${state.uri.queryParameters['sessionId']}'),
        ),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        aiSummarySessionsProvider.overrideWith((ref) async => items),
        sessionFavouritesProvider
            .overrideWith(() => _FixedFavourites(favourites)),
        mySessionsProvider.overrideWith(
          (ref) async => MyAreaSessions(mine.map(_mine).toList()),
        ),
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
}

void main() {
  group('SessionSummaryListScreen (Figma 1388:8392)', () {
    testWidgets('the All tab lists sessions; tapping one opens its summary',
        (tester) async {
      await _pump(
        tester,
        <SessionListItem>[_item('s1', 'Opening'), _item('s2', 'Closing')],
      );

      expect(find.text('Opening'), findsOneWidget);
      expect(find.text('Closing'), findsOneWidget);

      await tester.tap(find.text('Opening'));
      await tester.pumpAndSettle();
      expect(find.text('SUMMARY s1'), findsOneWidget);
    });

    testWidgets('the search field filters by title', (tester) async {
      await _pump(
        tester,
        <SessionListItem>[_item('s1', 'Opening'), _item('s2', 'Closing')],
      );

      await tester.enterText(find.byType(TextField), 'Clos');
      await tester.pumpAndSettle();
      expect(find.text('Closing'), findsOneWidget);
      expect(find.text('Opening'), findsNothing);
    });

    testWidgets('the Favourites tab shows only favourited sessions',
        (tester) async {
      await _pump(
        tester,
        <SessionListItem>[_item('s1', 'Opening'), _item('s2', 'Closing')],
        favourites: <String>{'s2'},
      );

      await tester.tap(find.text('Favourites'));
      await tester.pumpAndSettle();
      expect(find.text('Closing'), findsOneWidget);
      expect(find.text('Opening'), findsNothing);
    });

    testWidgets('the My sessions tab shows only booked sessions',
        (tester) async {
      await _pump(
        tester,
        <SessionListItem>[_item('s1', 'Opening'), _item('s2', 'Closing')],
        mine: <String>['s1'],
      );

      await tester.tap(find.text('My sessions'));
      await tester.pumpAndSettle();
      expect(find.text('Opening'), findsOneWidget);
      expect(find.text('Closing'), findsNothing);
    });

    testWidgets('shows the empty state when there are no sessions',
        (tester) async {
      await _pump(tester, const <SessionListItem>[]);
      expect(find.text('No sessions available yet.'), findsOneWidget);
    });
  });
}
