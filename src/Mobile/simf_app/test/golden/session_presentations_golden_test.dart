@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/sessions/data/presentation_models.dart';
import 'package:simf_app/features/sessions/data/presentation_repository.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart';
import 'package:simf_app/features/sessions/session_presentations_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the Session-presentations screen against Figma frame
/// **1388:7621** ("عروض الجلسات"). Compare goldens/presentations_1388-7621.png to
/// the frame — the day tabs (الكل / اليوم الأول / اليوم الثاني), and each deck
/// card: the navy file-icon box at the inline-end (left), the session title +
/// speaker on the right, the gold تحميل button (download glyph LEADING at the
/// inline-end/left of the label) on the bottom-left with the day label on the
/// bottom-right. Run:
///   flutter test --update-goldens test/golden/session_presentations_golden_test.dart

PresentationItem _item({
  required String id,
  required String titleAr,
  required String speakerAr,
  required DateTime start,
}) =>
    PresentationItem(
      id: id,
      sessionId: 's-$id',
      sessionTitle: titleAr,
      sessionTitleArabic: titleAr,
      sessionStart: start,
      speakerName: speakerAr,
      speakerNameArabic: speakerAr,
      fileName: '$id.pdf',
      contentType: 'application/pdf',
      sizeBytes: 2048,
    );

final _items = <PresentationItem>[
  _item(
    id: 'p1',
    titleAr: 'مستقبل الاستثمار الرقمي في المملكة',
    speakerAr: 'د. محمد العمري',
    start: DateTime.utc(2026, 11, 3, 6),
  ),
  _item(
    id: 'p2',
    titleAr: 'الذكاء الاصطناعي وتحول سوق العمل',
    speakerAr: 'أ. سارة الزهراني',
    start: DateTime.utc(2026, 11, 3, 7, 30),
  ),
  _item(
    id: 'p3',
    titleAr: 'رؤية 2030 ومسيرة التحول الاقتصادي',
    speakerAr: 'م. خالد الدوسري',
    start: DateTime.utc(2026, 11, 4, 9),
  ),
];

/// The programme behind each row, all with a published summary so the golden
/// locks the Figma frame's **active** gold تحميل buttons (owner 2026-07-14
/// gate).
SessionListItem _session(String sessionId) => SessionListItem(
      id: sessionId,
      code: 'C-$sessionId',
      title: 't',
      titleArabic: 't',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة',
      start: DateTime.utc(2026, 11, 3, 6),
      end: DateTime.utc(2026, 11, 3, 7),
      status: SessionStatus.scheduled,
      speakers: const <SessionSpeaker>[],
      hasPublishedSummary: true,
    );

final _sessions = <SessionListItem>[
  for (final p in _items) _session(p.sessionId),
];

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Session-presentations @375x812 — Figma 1388:7621 (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          presentationsProvider
              .overrideWith((ref) async => PresentationsPage(_items)),
          programmeSessionsProvider.overrideWith((ref) async => _sessions),
        ],
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          locale: const Locale('ar'),
          supportedLocales: AppL10n.supportedLocales,
          localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
            ...AppL10n.localizationsDelegates,
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],
          home: const SessionPresentationsScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(SessionPresentationsScreen),
      matchesGoldenFile('goldens/presentations_1388-7621.png'),
    );
  });
}
