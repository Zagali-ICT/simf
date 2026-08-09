@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart';
import 'package:simf_app/features/sessions/sessions_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the Sessions/programme screen against Figma frame
/// **883:2308** (برنامج الملتقى, public). Compare to the frame:
///   flutter test --update-goldens test/golden/sessions_golden_test.dart
///
/// Frame parity expected (D-598): the bordered search field; the WHITE day
/// strip (the programme days plus 2 muted neighbour days before the
/// first and after the last — the event centred, as the frame renders it; the
/// selected day is a navy pill, a session day navy text, a padding/empty day
/// muted grey, the weekend-red label only on active session days). On this
/// Arabic (RTL) render the day cells order right→left — the earliest programme
/// day on the right — and the strip scrolls from the right edge (owner
/// 2026-07-22, overriding the earlier 883:2327 LTR pin). Then the selected
/// day's own title ("تفاصيل اليوم" carries the
/// day title) over the day banner; the THREE type tabs الكل/جلسات/ورش العمل
/// (الكل active — احداث dropped per the 3-tab frame, owner 2026-07-03); then
/// the المواعيد list — the first session featured (expanded with the day
/// banner), the rest collapsed (LTR time rail + gold title + trailing gold
/// calendar glyph + grey description; no row numbers, no chevron). The page
/// itself is RTL; the day strip now follows it, while the time rails still pin
/// LTR like the frame.
///
/// The screen does NO date filtering (the day strip just spans the programme
/// days and selects days.first), so fixed dates render deterministically. The
/// day banner + featured image are Image.network → they fall back to the navy
/// anchor box in tests (no HTTP), which is the designed no-logo state.

SessionListItem _session({
  required String id,
  required String code,
  required String titleAr,
  required String titleEn,
  required String descAr,
  required DateTime start,
  required SessionType type,
}) =>
    SessionListItem(
      id: id,
      code: code,
      title: titleEn,
      titleArabic: titleAr,
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      start: start,
      end: start.add(const Duration(hours: 1, minutes: 30)),
      status: SessionStatus.scheduled,
      speakers: const <SessionSpeaker>[],
      type: type,
      descriptionArabic: descAr,
    );

final _days = <ProgrammeDay>[
  ProgrammeDay(
    id: 'd1',
    date: DateTime(2026, 6, 20),
    title: 'Day One',
    titleArabic: 'اليوم الأول',
    displayOrder: 0,
    hasImage: true,
    sessions: <SessionListItem>[
      _session(
        id: 's1',
        code: 'S-01',
        titleAr: 'مستقبل الأمن البحري',
        titleEn: 'The Future of Maritime Security',
        descAr:
            'نظرة معمّقة على مستقبل الأمن البحري والتحديات الإقليمية والدولية وأثرها على الممرات الملاحية.',
        start: DateTime.utc(2026, 6, 20, 5),
        type: SessionType.workshop,
      ),
      _session(
        id: 's2',
        code: 'S-02',
        titleAr: 'الاستراتيجيات الدفاعية الحديثة',
        titleEn: 'Modern Defence Strategies',
        descAr: 'استعراض أحدث المنظومات الدفاعية والشراكات الاستراتيجية في المنطقة.',
        start: DateTime.utc(2026, 6, 20, 7, 30),
        type: SessionType.session,
      ),
    ],
  ),
  ProgrammeDay(
    id: 'd2',
    date: DateTime(2026, 6, 21),
    title: 'Day Two',
    titleArabic: 'اليوم الثاني',
    displayOrder: 1,
    hasImage: false,
    sessions: <SessionListItem>[
      _session(
        id: 's3',
        code: 'E-01',
        titleAr: 'حفل الافتتاح',
        titleEn: 'Opening Ceremony',
        descAr: 'حفل افتتاح الملتقى البحري السعودي الدولي وكلمات الرعاة.',
        start: DateTime.utc(2026, 6, 21, 6),
        type: SessionType.event,
      ),
    ],
  ),
];

/// The screen only calls getDays(); getSessions() returns the flattened list for
/// completeness (unused by this frame).
class _FakeSessionsRepo implements SessionsRepository {
  @override
  Future<List<ProgrammeDay>> getDays() async => _days;

  @override
  Future<List<SessionListItem>> getSessions() async =>
      _days.expand((d) => d.sessions).toList(growable: false);
}

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Sessions @375x1300 — Figma 883:2308 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 1300);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/sessions',
      routes: <RouteBase>[
        GoRoute(
          path: '/sessions',
          name: RouteNames.sessions,
          builder: (_, __) => const SessionsScreen(),
        ),
        GoRoute(
          path: '/sessions/:sessionId',
          name: RouteNames.sessionDetail,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_config),
          sessionsRepositoryProvider.overrideWithValue(_FakeSessionsRepo()),
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
      find.byType(SessionsScreen),
      matchesGoldenFile('goldens/sessions_883-2308.png'),
    );
  });
}
