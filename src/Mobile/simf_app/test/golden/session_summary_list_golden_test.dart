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
import 'package:simf_app/features/ai_summary/session_summary_list_screen.dart';
import 'package:simf_app/features/myarea/data/my_sessions_models.dart';
import 'package:simf_app/features/myarea/data/my_sessions_repository.dart';
import 'package:simf_app/features/sessions/data/session_favourites.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart'
    show programmeSessionsProvider;
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Golden render of the Session-summaries list against Figma frame **1388:8392**
/// (ملخص الجلسات, Guest+). Compare to the frame:
///   flutter test --update-goldens test/golden/session_summary_list_golden_test.dart
///
/// Frame parity: the search+filter field, the 3 equal-width chips
/// (المفضلة/جلساتي/الجميع — الجميع selected on the right), day group headers
/// (اليوم الأول/الثاني), and rich cards — heart top-left, title + clock·time·
/// duration on the right, speaker·role (right) + hall (left) meta, and a bottom
/// row with the gold مسجّل badge (left) + bordered category pill (right). RTL.

SessionListItem _session({
  required String id,
  required String titleAr,
  required String titleEn,
  required DateTime startUtc,
  required int durationMin,
  required String hallAr,
  required String categoryAr,
  required String speakerAr,
  required String speakerTitleAr,
  required SessionStatus status,
}) =>
    SessionListItem(
      id: id,
      code: id,
      title: titleEn,
      titleArabic: titleAr,
      hallId: 'h-$id',
      hallName: hallAr,
      hallNameArabic: hallAr,
      startUtc: startUtc,
      endUtc: startUtc.add(Duration(minutes: durationMin)),
      status: status,
      categoryName: categoryAr,
      categoryNameArabic: categoryAr,
      // The summaries list only holds sessions with a published محضر (owner
      // 2026-07-14); these layout fixtures are all summarised so the golden
      // keeps rendering the 4 cards.
      hasPublishedSummary: true,
      speakers: <SessionSpeaker>[
        SessionSpeaker(
          id: 'sp-$id',
          name: speakerAr,
          nameArabic: speakerAr,
          title: speakerTitleAr,
          displayOrder: 0,
          role: SessionSpeakerRole.speaker,
        ),
      ],
    );

final _sessions = <SessionListItem>[
  // اليوم الأول
  _session(
    id: 's1',
    titleAr: 'أمن سلاسل إمداد الطاقة البحرية',
    titleEn: 'Maritime Energy Supply-Chain Security',
    startUtc: DateTime.utc(2026, 6, 20, 6, 0),
    durationMin: 60,
    hallAr: 'قاعة الملك فهد',
    categoryAr: 'الاقتصاد الرقمي',
    speakerAr: 'د. محمد العمري',
    speakerTitleAr: 'رئيس هيئة الاستثمار',
    status: SessionStatus.recorded,
  ),
  _session(
    id: 's2',
    titleAr: 'الذكاء الاصطناعي وتحول سوق العمل',
    titleEn: 'AI and the Future of Work',
    startUtc: DateTime.utc(2026, 6, 20, 7, 30),
    durationMin: 45,
    hallAr: 'قاعة الأمير سلطان',
    categoryAr: 'التقنية والابتكار',
    speakerAr: 'أ. سارة الزهراني',
    speakerTitleAr: 'مديرة مركز الابتكار',
    status: SessionStatus.scheduled,
  ),
  // اليوم الثاني
  _session(
    id: 's3',
    titleAr: 'رؤية 2030 ومسيرة التحول الاقتصادي',
    titleEn: 'Vision 2030 and Economic Transformation',
    startUtc: DateTime.utc(2026, 6, 21, 9, 0),
    durationMin: 90,
    hallAr: 'القاعة الرئيسية',
    categoryAr: 'الجلسة الافتتاحية',
    speakerAr: 'م. خالد الدوسري',
    speakerTitleAr: 'مستشار وزارة الاقتصاد',
    status: SessionStatus.recorded,
  ),
  _session(
    id: 's4',
    titleAr: 'ريادة الأعمال في عصر الاضطراب',
    titleEn: 'Entrepreneurship in an Age of Disruption',
    startUtc: DateTime.utc(2026, 6, 21, 11, 0),
    durationMin: 60,
    hallAr: 'قاعة الملك فهد',
    categoryAr: 'ريادة الأعمال',
    speakerAr: 'نورة الشمري',
    speakerTitleAr: 'مؤسسة شركة ابتكار',
    status: SessionStatus.recorded,
  ),
];

/// Seeds the favourites set so the first card's heart reads filled.
class _FakeFavourites extends SessionFavouritesController {
  _FakeFavourites(this._ids);

  final Set<String> _ids;

  @override
  Future<Set<String>> build() async => _ids;
}

const _config = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Session-summaries @375x1150 — Figma 1388:8392 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 1150);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

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
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: <Override>[
          simfDataConfigProvider.overrideWithValue(_config),
          programmeSessionsProvider.overrideWith((ref) async => _sessions),
          mySessionsProvider
              .overrideWith((ref) async => const MyAreaSessions(<MyAreaSessionItem>[])),
          sessionFavouritesProvider
              .overrideWith(() => _FakeFavourites(<String>{'s1'})),
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
      find.byType(SessionSummaryListScreen),
      matchesGoldenFile('goldens/session_summary_list_1388-8392.png'),
    );
  });
}
