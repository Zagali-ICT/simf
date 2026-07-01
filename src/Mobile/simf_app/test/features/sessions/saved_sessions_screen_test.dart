import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/ai_summary/session_summary_screen.dart'
    show aiSummarySessionsProvider;
import 'package:simf_app/features/sessions/data/session_favourites.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/saved_sessions_screen.dart';

/// A favourites controller seeded with a fixed set (no API).
class _FixedFavourites extends SessionFavouritesController {
  _FixedFavourites(this._ids);

  final Set<String> _ids;

  @override
  Future<Set<String>> build() async => _ids;
}

SessionListItem _item(String id, String title, {String? category}) =>
    SessionListItem(
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
      categoryName: category,
      categoryNameArabic: category,
    );

Future<void> _pump(
  WidgetTester tester,
  List<SessionListItem> items, {
  Set<String> favourites = const <String>{},
}) async {
  final router = GoRouter(
    initialLocation: '/saved-sessions',
    routes: <RouteBase>[
      GoRoute(
        path: '/saved-sessions',
        name: RouteNames.savedSessions,
        builder: (_, __) => const SavedSessionsScreen(),
      ),
      GoRoute(
        path: '/sessions/:sessionId',
        name: RouteNames.sessionDetail,
        builder: (_, state) =>
            Scaffold(body: Text('DETAIL ${state.pathParameters['sessionId']}')),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        aiSummarySessionsProvider.overrideWith((ref) async => items),
        sessionFavouritesProvider
            .overrideWith(() => _FixedFavourites(favourites)),
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
  group('SavedSessionsScreen (Figma 1701:8928)', () {
    testWidgets('lists only the favourited sessions', (tester) async {
      await _pump(
        tester,
        <SessionListItem>[_item('s1', 'Opening'), _item('s2', 'Closing')],
        favourites: <String>{'s2'},
      );

      expect(find.text('Closing'), findsOneWidget);
      expect(find.text('Opening'), findsNothing);
    });

    testWidgets('the count row shows the saved count', (tester) async {
      await _pump(
        tester,
        <SessionListItem>[
          _item('s1', 'Opening'),
          _item('s2', 'Closing'),
          _item('s3', 'Keynote'),
        ],
        favourites: <String>{'s1', 's3'},
      );

      expect(find.text('2'), findsOneWidget); // saved count
      expect(find.text('saved sessions'), findsOneWidget); // unit label
    });

    testWidgets('tapping a card opens the session detail', (tester) async {
      await _pump(
        tester,
        <SessionListItem>[_item('s1', 'Opening')],
        favourites: <String>{'s1'},
      );

      await tester.tap(find.text('Opening'));
      await tester.pumpAndSettle();
      expect(find.text('DETAIL s1'), findsOneWidget);
    });

    testWidgets('the category chips filter the list', (tester) async {
      await _pump(
        tester,
        <SessionListItem>[
          _item('s1', 'Green', category: 'Environment'),
          _item('s2', 'Power', category: 'Energy'),
        ],
        favourites: <String>{'s1', 's2'},
      );

      // Both show under the default الكل / All chip.
      expect(find.text('Green'), findsOneWidget);
      expect(find.text('Power'), findsOneWidget);

      await tester.tap(find.text('Energy'));
      await tester.pumpAndSettle();
      expect(find.text('Power'), findsOneWidget);
      expect(find.text('Green'), findsNothing);
    });

    testWidgets('shows the empty state when nothing is saved', (tester) async {
      await _pump(tester, <SessionListItem>[_item('s1', 'Opening')]);
      expect(find.text('No saved sessions yet.'), findsOneWidget);
    });
  });
}
