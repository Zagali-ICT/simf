@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart'
    show programmeSessionsProvider;
import 'package:simf_app/features/sessions/join_session_hub_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Render-lock golden of the Join-a-session hub (D-485 — no Figma frame of its
/// own; a plain session list into the join flow). Unsuffixed filename =
/// render-locked, not frame-locked.
/// flutter test --update-goldens test/golden/join_session_hub_golden_test.dart
///
/// Render expected: the centred beige hint line, then navy SimfCard rows —
/// title over the "time · hall" line with the direction-aware chevron
/// (left-pointing under RTL) — with pull-to-refresh (added D-601). RTL.

SessionListItem _item(String id, String titleAr, {required int hourLocal}) =>
    SessionListItem(
      id: id,
      code: 'C-$id',
      title: titleAr,
      titleArabic: titleAr,
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      // LOCAL instants: the row renders TimeOfDay.fromDateTime(startLocal) —
      // a a zoned value fixture would bake the generating machine's timezone
      // into the
      // PNG (same trap as the saved-sessions golden, D-599).
      start: DateTime(2026, 11, 23, hourLocal),
      end: DateTime(2026, 11, 23, hourLocal + 1),
      status: SessionStatus.scheduled,
      speakers: const <SessionSpeaker>[],
    );

final _sessions = <SessionListItem>[
  _item('s1', 'مستقبل الأمن البحري', hourLocal: 9),
  _item('s2', 'التحول الرقمي في الدفاع', hourLocal: 11),
  _item('s3', 'الصناعات البحرية الوطنية', hourLocal: 13),
];

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Join-session hub @375x812 — render-lock (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/sessions/join',
      routes: <RouteBase>[
        GoRoute(
          path: '/sessions/join',
          name: RouteNames.joinSessionHub,
          builder: (_, __) => const JoinSessionHubScreen(),
        ),
        GoRoute(
          path: '/sessions/:sessionId',
          name: RouteNames.sessionDetail,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          programmeSessionsProvider.overrideWith((ref) async => _sessions),
        ],
        child: MaterialApp.router(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          routerConfig: router,
          locale: const Locale('ar'),
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

    await expectLater(
      find.byType(JoinSessionHubScreen),
      matchesGoldenFile('goldens/join_session_hub.png'),
    );
  });
}
