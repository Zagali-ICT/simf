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
import 'package:simf_app/features/ai_summary/data/session_summary_models.dart';
import 'package:simf_app/features/ai_summary/data/session_summary_repository.dart';
import 'package:simf_app/features/ai_summary/session_summary_screen.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart'
    show programmeSessionsProvider;
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the redesigned Session-summary screen against Figma frame
/// **1072:13518** (ملخص الجلسة). Compare to the frame:
///   flutter test --update-goldens test/golden/session_summary_golden_test.dart
///
/// Frame parity: the "الجلسة" info card (gold title + day·time·duration·hall
/// over
/// the day-agenda timeline), the 3-tab segmented control (أبرز النقاط active),
/// the
/// tab-content card with the gold-bar heading + gold-dot bullets, and the gold
/// "توليد ملخص للجلسة" button over the expanded summary paragraph. RTL.

SessionListItem _session({
  required String id,
  required String titleAr,
  required DateTime start,
  int durationMin = 60,
}) =>
    SessionListItem.fromJson(<String, dynamic>{
      'id': id,
      'code': id,
      'title': titleAr,
      'titleArabic': titleAr,
      'hallId': 'h1',
      'hallName': 'King Fahd Hall',
      'hallNameArabic': 'قاعة الملك فهد',
      'start': start.toIso8601String(),
      'end': start.add(Duration(minutes: durationMin)).toIso8601String(),
      'speakers': const <dynamic>[],
    });

// 2026-06-21 is a Sunday (الأحد). a zoned value times are chosen so a +03:00
// render shows
// the frame's 09:00 / 11:00 / 13:30 / 16:00; the render stays deterministic on
// any single machine regardless.
final _sessions = <SessionListItem>[
  _session(
    id: 'sel',
    titleAr: 'أمن سلاسل إمداد الطاقة البحرية',
    start: DateTime.utc(2026, 6, 21, 6),
  ),
  _session(
    id: 'a1',
    titleAr: 'الافتتاح والترحيب',
    start: DateTime.utc(2026, 6, 21, 6),
  ),
  _session(
    id: 'a2',
    titleAr: 'حماية قاع البحار',
    start: DateTime.utc(2026, 6, 21, 8),
  ),
  _session(
    id: 'a3',
    titleAr: 'الأمن السيبراني البحري',
    start: DateTime.utc(2026, 6, 21, 10, 30),
  ),
  _session(
    id: 'a4',
    titleAr: 'جلسة الختام والتوصيات',
    start: DateTime.utc(2026, 6, 21, 13),
  ),
];

SessionSummary _summary() => SessionSummary.fromJson(const <String, dynamic>{
      'keyPointsArabic':
          'حماية منظومات الطاقة الممتدة عبر البحار من خطوط وأنابيب النفط '
              'والغاز.\n'
              'أهمية الممرات البحرية الحيوية في ظل تصاعد التهديدات '
                  'الجيوسياسية.\n'
              'دور التقنيات الحديثة والذكاء الاصطناعي في رصد المخاطر مبكرًا.',
      'recommendationsArabic': 'توسيع برامج الرصد البحري المشترك.',
      'speakersArabic': 'د. محمد العمري · العميد سالم',
      'fullTextArabic':
          'منصة الملتقى البحري السعودي الدولي حدث دولي رفيع المستوى، يجمع '
              'القادة '
              'والمسؤولين والخبراء لتبادل التجارب وتعزيز فهم مشترك لمستقبل '
                  'الأمن البحري.',
      'generatedByAi': true,
      'publishedAt': '2026-06-21T07:00:00Z',
    });

class _FakeSummaryRepo implements SessionSummaryRepository {
  @override
  Future<SessionSummary> getSummary(String sessionId) async => _summary();
}

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Session-summary @375x1280 — Figma 1072:13518 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 1280);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/ai-summary',
      routes: <RouteBase>[
        GoRoute(
          path: '/ai-summary',
          name: RouteNames.aiSummary,
          builder: (_, __) => const AiSummaryScreen(sessionId: 'sel'),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_config),
          sessionSummaryRepositoryProvider
              .overrideWithValue(_FakeSummaryRepo()),
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
      find.byType(AiSummaryScreen),
      matchesGoldenFile('goldens/session_summary_1072-13518.png'),
    );
  });
}
