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
import 'package:simf_app/features/archive/archive_screen.dart';
import 'package:simf_app/features/archive/data/archive_models.dart';
import 'package:simf_app/features/archive/data/archive_repository.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden-render harness (owner 2026-06-28, verification option 2): renders the
/// Archive screen at its exact Figma frame size (375×1293, node 925:3079) with
/// the real brand fonts loaded, so the generated PNG can be compared
/// pixel-for-pixel against the Figma frame. Run:
///   flutter test --update-goldens test/golden/archive_golden_test.dart
/// then open test/golden/goldens/archive_925-3079.png and compare to Figma.

// Three editions — newest-first from the API. In the RTL pill row the first
// child sits at the inline start (right), so [2024, 2022, 2020] renders as
// 2020 · 2022 · 2024 left→right (matching the frame), 2024 selected by default.
const _editions = <ArchiveEdition>[
  ArchiveEdition(
    id: 'a2024',
    year: 2024,
    titleEn: 'Saudi International Maritime Forum 2024',
    titleAr: 'الملتقى البحري السعودي الدولي 2024',
    attendees: 1200,
    sessions: 30,
    speakers: 250,
    summaryAr: 'منصة دولية جمعت قادة القوات البحرية والخبراء لمناقشة مستقبل '
        'الأمن البحري وحماية الممرات الملاحية.',
  ),
  ArchiveEdition(
    id: 'a2022',
    year: 2022,
    titleEn: '2022 Edition',
    titleAr: 'نسخة 2022',
    attendees: 900,
    sessions: 18,
    speakers: 120,
  ),
  ArchiveEdition(
    id: 'a2020',
    year: 2020,
    titleEn: '2020 Edition',
    titleAr: 'نسخة 2020',
    attendees: 600,
    sessions: 12,
    speakers: 80,
  ),
];

final _detail2024 = ArchiveEditionDetail(
  id: 'a2024',
  year: 2024,
  titleEn: 'Saudi International Maritime Forum 2024',
  titleAr: 'الملتقى البحري السعودي الدولي 2024',
  attendees: 1200,
  sessions: 30,
  speakers: 250,
  summaryAr: 'منصة دولية جمعت قادة القوات البحرية والخبراء لمناقشة مستقبل '
      'الأمن البحري وحماية الممرات الملاحية.',
  locationAr: 'الرياض · واجهة الرياض',
  dateLabelAr: 'نوفمبر 2024 · 3 أيام',
  gallery: const <ArchiveMediaItem>[
    ArchiveMediaItem(kind: 0, url: 'https://cdn.example.sa/2024/a.jpg'),
    ArchiveMediaItem(kind: 0, url: 'https://cdn.example.sa/2024/b.jpg'),
    ArchiveMediaItem(kind: 1, url: 'https://youtu.be/abc'),
  ],
  sessionTitles: const <ArchiveSessionTitle>[
    ArchiveSessionTitle(
      titleEn: 'Opening session',
      titleAr: 'الجلسة الافتتاحية: مستقبل الأمن البحري',
    ),
    ArchiveSessionTitle(
      titleEn: 'Lanes & ports',
      titleAr: 'حماية الممرات والموانئ',
    ),
    ArchiveSessionTitle(
      titleEn: 'Modern maritime tech',
      titleAr: 'التقنيات البحرية الحديثة',
    ),
  ],
  // 15 → the row shows the first three + a "+12 / آخرون" overflow tile.
  pastSpeakers: <ArchivePastSpeaker>[
    const ArchivePastSpeaker(nameEn: 'M. Ahmed', nameAr: 'م. أحمد'),
    const ArchivePastSpeaker(nameEn: 'D. Khaled', nameAr: 'د. خالد'),
    const ArchivePastSpeaker(nameEn: 'A. Ali', nameAr: 'أ. على'),
    for (var i = 0; i < 12; i++)
      ArchivePastSpeaker(nameEn: 'Speaker $i', nameAr: 'متحدث $i'),
  ],
);

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Archive @375x1293 — Figma 925:3079 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 1293);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/archive',
      routes: <RouteBase>[
        GoRoute(
          path: '/archive',
          name: RouteNames.archive,
          builder: (_, __) => const ArchiveScreen(),
        ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          archiveEditionsProvider.overrideWith((ref) async => _editions),
          archiveEditionDetailProvider('a2024')
              .overrideWith((ref) async => _detail2024),
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
      find.byType(ArchiveScreen),
      matchesGoldenFile('goldens/archive_925-3079.png'),
    );
  });
}
