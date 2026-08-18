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
import 'package:simf_app/features/faq/data/faq_models.dart';
import 'package:simf_app/features/faq/data/faq_repository.dart';
import 'package:simf_app/features/faq/faq_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the FAQ accordion against Figma frame **1388:7567**
/// (الأسئلة الشائعة). Regenerate: flutter test --update-goldens
/// test/golden/faq_golden_test.dart
///
/// Frame parity: the navy `SimfPageShell` (back + centred title + hairline)
/// over navyDeep `SimfCard` accordion rows — a beige Medium-14 question
/// (`SimfTokens.labelBeigeMedium`) with a gold expand chevron; the first card
/// is expanded to show the hairline divider + the answer (beige 14, line-height
/// 1.5 — `SimfTokens.bodyBeige`). A single group renders the flat accordion (no
/// section header). Locks the D-517 parity after the Phase-0g token extraction.
final _groups = <FaqGroup>[
  const FaqGroup(
    id: 'g1',
    name: '',
    nameArabic: 'عام',
    entries: <FaqEntry>[
      FaqEntry(
        id: 'q1',
        question: '',
        questionArabic: 'متى يبدأ الملتقى؟',
        answer: '',
        answerArabic: 'يبدأ الملتقى في 23 نوفمبر 2026 ويستمر ثلاثة أيام.',
      ),
      FaqEntry(
        id: 'q2',
        question: '',
        questionArabic: 'هل التسجيل مجاني؟',
        answer: '',
        answerArabic: 'نعم، التسجيل في الملتقى مجاني لجميع الزوار المسجّلين.',
      ),
      FaqEntry(
        id: 'q3',
        question: '',
        questionArabic: 'أين يقام الملتقى؟',
        answer: '',
        answerArabic: 'يقام في مركز الملك عبدالله الدولي للمؤتمرات بالرياض.',
      ),
    ],
  ),
];

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('FAQ @375x900 — Figma 1388:7567 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/faq',
      routes: <RouteBase>[
        GoRoute(
          path: '/faq',
          name: RouteNames.faq,
          builder: (_, __) => const FaqScreen(),
        ),
        GoRoute(
          path: '/',
          name: RouteNames.home,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          faqProvider.overrideWith((ref) => _groups),
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

    // Expand the first question so the answer style is in frame.
    await tester.tap(find.text('متى يبدأ الملتقى؟'));
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(FaqScreen),
      matchesGoldenFile('goldens/faq_1388-7567.png'),
    );
  });
}
