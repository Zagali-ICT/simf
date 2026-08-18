@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/feedback/data/feedback_repository.dart';
import 'package:simf_app/features/feedback/data/rating_models.dart';
import 'package:simf_app/features/feedback/rate_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the Rate screen against Figma frame **1116:16894** (تقييم
/// الملتقى). Regenerate: flutter test --update-goldens
/// test/golden/rate_golden_test.dart
///
/// Frame parity expected: the overall kicker + lead + the big 30px star bar,
/// then the "قيّم العناصر" section over per-element rows (each a beige-hairline
/// box: the element name at the inline-start, an 18px star bar at the
/// inline-end), the comment box, and the full-width gold submit button. RTL,
/// unscored (all outline stars) so the PNG is stable.

class _FormRepo implements FeedbackRepository {
  @override
  Future<RatingFormView> getForm({
    String? code,
    String? ratingTypeId,
    String? targetId,
  }) async =>
      const RatingFormView(
        ratingTypeId: 'type-app',
        code: 'App',
        name: 'App',
        nameArabic: 'التطبيق',
        hasOverallStars: true,
        allowComment: true,
        commentLabel: null,
        commentLabelArabic: null,
        targetId: null,
        groups: <RatingFormGroup>[],
        ungroupedQuestions: <RatingFormQuestion>[
          RatingFormQuestion(
            id: 'q-org',
            text: 'Organization',
            textArabic: 'التنظيم',
            isRequired: false,
          ),
          RatingFormQuestion(
            id: 'q-venue',
            text: 'Venue',
            textArabic: 'المكان',
            isRequired: false,
          ),
          RatingFormQuestion(
            id: 'q-content',
            text: 'Content',
            textArabic: 'المحتوى',
            isRequired: false,
          ),
        ],
        existing: null,
      );

  @override
  Future<void> submit({
    required String ratingTypeId,
    required Map<String, int> answers, String? targetId,
    int? overallStars,
    String? comment,
  }) async {}
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Rate @375x1100 — Figma 1116:16894 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 1100);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          feedbackRepositoryProvider.overrideWithValue(_FormRepo()),
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
          home: const RateScreen(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(RateScreen),
      matchesGoldenFile('goldens/rate_1116-16894.png'),
    );
  });
}
