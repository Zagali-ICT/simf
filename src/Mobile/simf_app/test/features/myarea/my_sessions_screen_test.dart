import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/myarea/data/my_sessions_models.dart';
import 'package:simf_app/features/myarea/data/my_sessions_repository.dart';
import 'package:simf_app/features/myarea/my_sessions_screen.dart';
import 'package:simf_app/features/sessions/data/session_favourites.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';

import '../../support/simf_test_scope.dart';

class _FixedFavourites extends SessionFavouritesController {
  _FixedFavourites(this._ids);

  final Set<String> _ids;

  @override
  Future<Set<String>> build() async => _ids;
}

MyAreaSessionItem _session({
  required String id,
  required String title,
  required bool attended,
  bool upcoming = true,
  SessionStatus status = SessionStatus.scheduled,
}) =>
    MyAreaSessionItem(
      id: id,
      title: title,
      titleArabic: title,
      // upcoming → far future (always "upcoming"); else far past (always
      // ended).
      start:
          upcoming ? DateTime.utc(2099, 1, 1, 6) : DateTime.utc(2020, 1, 1, 6),
      end: upcoming ? DateTime.utc(2099, 1, 1, 7) : DateTime.utc(2020, 1, 1, 7),
      status: status,
      attended: attended,
      isFavourite: false,
      hallNameEn: 'Main Hall',
      hallNameAr: 'القاعة',
      categoryNameEn: 'Economy',
      categoryNameAr: 'اقتصاد',
      speakerNameEn: 'Dr. Omari',
      speakerNameAr: 'د. العمري',
      speakerTitle: 'Chair',
    );

Future<void> _pump(WidgetTester tester, List<MyAreaSessionItem> items) async {
  final router = GoRouter(
    initialLocation: '/my-area/sessions',
    routes: <RouteBase>[
      GoRoute(
        path: '/my-area/sessions',
        name: RouteNames.myAreaSessions,
        builder: (_, __) => const MySessionsScreen(),
      ),
      GoRoute(
        path: '/sessions/:sessionId',
        name: RouteNames.sessionDetail,
        builder: (_, state) => Scaffold(
          body: Text('DETAIL ${state.pathParameters['sessionId']}'),
        ),
      ),
    ],
  );
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        mySessionsProvider.overrideWith((ref) async => MyAreaSessions(items)),
        sessionFavouritesProvider
            .overrideWith(() => _FixedFavourites(const <String>{})),
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
  group('MySessionsScreen (Figma 1388:9067)', () {
    testWidgets('the Upcoming tab lists the session; tap opens its detail',
        (tester) async {
      await _pump(tester, <MyAreaSessionItem>[
        _session(id: 's1', title: 'Opening', attended: false),
      ]);

      expect(find.text('Opening'), findsOneWidget);

      await tester.tap(find.text('Opening'));
      await tester.pumpAndSettle();
      expect(find.text('DETAIL s1'), findsOneWidget);
    });

    testWidgets('the Attended tab partitions on the attended flag',
        (tester) async {
      await _pump(tester, <MyAreaSessionItem>[
        // Attended + ended → only on حضرتها; not-attended + future → only on
        // القادمة.
        _session(id: 's1', title: 'WentTo', attended: true, upcoming: false),
        _session(id: 's2', title: 'ToCome', attended: false),
      ]);

      // Default Upcoming tab shows only the future, not-attended one.
      expect(find.text('ToCome'), findsOneWidget);
      expect(find.text('WentTo'), findsNothing);

      await tester.tap(find.text('Attended'));
      await tester.pumpAndSettle();
      expect(find.text('WentTo'), findsOneWidget);
      expect(find.text('ToCome'), findsNothing);
    });

    testWidgets('shows the empty state when a tab has no sessions',
        (tester) async {
      await _pump(tester, const <MyAreaSessionItem>[]);
      expect(find.text('No sessions in this list.'), findsOneWidget);
    });
  });
}
