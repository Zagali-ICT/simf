import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/feedback/data/feedback_repository.dart';
import 'package:simf_app/features/feedback/data/rating_models.dart';
import 'package:simf_app/features/feedback/rate_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// A fake repository that returns a configured form and captures the submission.
class _FakeFeedbackRepo implements FeedbackRepository {
  _FakeFeedbackRepo({this.withQuestion = false, this.failSubmit = false});

  final bool withQuestion;
  final bool failSubmit;

  int? lastOverall;
  Map<String, int>? lastAnswers;
  String? lastComment;

  @override
  Future<RatingFormView> getForm({
    String? code,
    String? ratingTypeId,
    String? targetId,
  }) async {
    return RatingFormView(
      ratingTypeId: 'type-app',
      code: 'App',
      name: 'App',
      nameArabic: 'التطبيق',
      hasOverallStars: true,
      allowComment: true,
      commentLabel: null,
      commentLabelArabic: null,
      targetId: null,
      groups: const <RatingFormGroup>[],
      ungroupedQuestions: withQuestion
          ? const <RatingFormQuestion>[
              RatingFormQuestion(
                id: 'q-org',
                text: 'Organization',
                textArabic: 'التنظيم',
                isRequired: false,
              ),
            ]
          : const <RatingFormQuestion>[],
      existing: null,
    );
  }

  @override
  Future<void> submit({
    required String ratingTypeId,
    String? targetId,
    int? overallStars,
    String? comment,
    required Map<String, int> answers,
  }) async {
    lastOverall = overallStars;
    lastAnswers = answers;
    lastComment = comment;
    if (failSubmit) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
  }
}

Future<void> _pump(WidgetTester tester, FeedbackRepository repo) async {
  tester.view.physicalSize = const Size(375, 1800);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  final router = GoRouter(
    initialLocation: '/rate',
    routes: <RouteBase>[
      GoRoute(path: '/rate', builder: (_, __) => const RateScreen()),
      for (final (name, path) in <(String, String)>[
        (RouteNames.home, '/'),
        (RouteNames.sessions, '/sessions'),
        (RouteNames.badge, '/badge'),
        (RouteNames.venueMap, '/map'),
        (RouteNames.myArea, '/my-area'),
      ])
        GoRoute(
          name: name,
          path: path,
          builder: (c, s) => Scaffold(body: Text(name)),
        ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[feedbackRepositoryProvider.overrideWithValue(repo)],
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

final _outlineStar = find.byIcon(Icons.star_outline_rounded);

Finder _categoryStars(String label) => find.descendant(
      of: find.ancestor(of: find.text(label), matching: find.byType(Row)).first,
      matching: find.byIcon(Icons.star_outline_rounded),
    );

void main() {
  group('RateScreen (Page 040 — dynamic form)', () {
    testWidgets('submitting with no overall stars prompts for a rating',
        (tester) async {
      final repo = _FakeFeedbackRepo();
      await _pump(tester, repo);
      await tester.tap(find.text('Submit rating'));
      await tester.pumpAndSettle();
      expect(find.text('Please pick a star rating'), findsOneWidget);
      expect(repo.lastOverall, isNull);
    });

    testWidgets('picking the overall stars + submit sends the rating',
        (tester) async {
      final repo = _FakeFeedbackRepo();
      await _pump(tester, repo);
      // The overall star bar is the first in the tree → its 4th star = 4.
      await tester.tap(_outlineStar.at(3));
      await tester.pumpAndSettle();
      expect(find.text('4 of 5 · Very good'), findsOneWidget);
      await tester.tap(find.text('Submit rating'));
      await tester.pumpAndSettle();
      expect(repo.lastOverall, 4);
      expect(repo.lastAnswers, isEmpty);
      expect(find.text('Thanks for your rating'), findsOneWidget);
    });

    testWidgets('a per-question score is sent alongside the overall stars',
        (tester) async {
      final repo = _FakeFeedbackRepo(withQuestion: true);
      await _pump(tester, repo);
      await tester.tap(_outlineStar.at(2)); // overall = 3
      await tester.pumpAndSettle();
      await tester.tap(_categoryStars('Organization').at(4)); // question = 5
      await tester.pumpAndSettle();
      await tester.tap(find.text('Submit rating'));
      await tester.pumpAndSettle();
      expect(repo.lastOverall, 3);
      expect(repo.lastAnswers, <String, int>{'q-org': 5});
    });

    testWidgets('a submit failure shows the error toast', (tester) async {
      await _pump(tester, _FakeFeedbackRepo(failSubmit: true));
      await tester.tap(_outlineStar.first); // overall = 1
      await tester.pumpAndSettle();
      await tester.tap(find.text('Submit rating'));
      await tester.pumpAndSettle();
      expect(find.text('Could not submit. Try again.'), findsOneWidget);
    });
  });
}
