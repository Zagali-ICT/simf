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
  _FakeFeedbackRepo({
    this.withQuestion = false,
    this.failSubmit = false,
    this.session = false,
    this.requiredQuestion = false,
    this.eligible = true,
  });

  final bool withQuestion;
  final bool failSubmit;

  /// Owner 2026-07-19 — false renders the "attend to rate" note + disables submit
  /// (mirrors the server form's isEligible=false).
  final bool eligible;

  bool submitCalled = false;

  /// Returns a per-session form (code=Session, with a target) instead of the
  /// default App form.
  final bool session;

  /// The form's single question is `isRequired` — used to exercise the
  /// client-side "must score every required question" block.
  final bool requiredQuestion;

  int? lastOverall;
  Map<String, int>? lastAnswers;
  String? lastComment;

  @override
  Future<RatingFormView> getForm({
    String? code,
    String? ratingTypeId,
    String? targetId,
  }) async {
    final hasQuestion = withQuestion || requiredQuestion;
    return RatingFormView(
      ratingTypeId: session ? 'type-session' : 'type-app',
      code: session ? 'Session' : 'App',
      name: session ? 'Session' : 'App',
      nameArabic: session ? 'الجلسة' : 'التطبيق',
      hasOverallStars: true,
      allowComment: true,
      commentLabel: null,
      commentLabelArabic: null,
      targetId: session ? (targetId ?? 'sess-1') : null,
      // D-713 — the per-session form carries the watched-at context; the App
      // form does not. A UTC start keeps the fixture timezone-independent (the
      // header assertions check the session name, not the tz-shifted time).
      targetName: session ? 'Opening Session' : null,
      targetNameArabic: session ? 'الجلسة الافتتاحية' : null,
      targetStart: session ? DateTime.utc(2026, 11, 20, 9) : null,
      groups: const <RatingFormGroup>[],
      ungroupedQuestions: hasQuestion
          ? <RatingFormQuestion>[
              RatingFormQuestion(
                id: session ? 'q-speaker' : 'q-org',
                text: session ? 'Speaker' : 'Organization',
                textArabic: session ? 'المتحدث' : 'التنظيم',
                isRequired: requiredQuestion,
              ),
            ]
          : const <RatingFormQuestion>[],
      existing: null,
      isEligible: eligible,
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
    submitCalled = true;
    lastOverall = overallStars;
    lastAnswers = answers;
    lastComment = comment;
    if (failSubmit) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
  }
}

Future<void> _pump(
  WidgetTester tester,
  FeedbackRepository repo, {
  Widget rateScreen = const RateScreen(),
}) async {
  tester.view.physicalSize = const Size(375, 1800);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);

  final router = GoRouter(
    initialLocation: '/rate',
    routes: <RouteBase>[
      GoRoute(path: '/rate', builder: (_, __) => rateScreen),
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

    testWidgets('an ineligible form shows the attend note + disables submit '
        '(owner 2026-07-19)', (tester) async {
      final repo = _FakeFeedbackRepo(eligible: false);
      await _pump(tester, repo);

      // The "you can only rate what you attended" note is shown.
      expect(
        find.text('You can only rate something you attended.'),
        findsOneWidget,
      );

      // Submit is disabled — even after picking stars, tapping it neither
      // submits nor shows the thanks toast (onTap is null when ineligible).
      await tester.tap(_outlineStar.at(3));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Submit rating'));
      await tester.pumpAndSettle();
      expect(repo.submitCalled, isFalse);
      expect(find.text('Thanks for your rating'), findsNothing);
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

    testWidgets('a per-session rating shows the watched-at header',
        (tester) async {
      // D-713 (item 8) — the per-session rate screen leads with a "Watched
      // {session} · {date}" context line so the user knows what they're rating.
      await _pump(
        tester,
        _FakeFeedbackRepo(session: true),
        rateScreen: const RateScreen(code: 'Session', targetId: 'sess-1'),
      );
      expect(find.textContaining('Opening Session'), findsOneWidget);
    });

    testWidgets('the global App rating shows no watched-at header',
        (tester) async {
      // A Global (App) rating has no target session, so no context header.
      await _pump(tester, _FakeFeedbackRepo());
      expect(find.textContaining('Watched'), findsNothing);
    });

    testWidgets('a submit failure shows the error toast', (tester) async {
      await _pump(tester, _FakeFeedbackRepo(failSubmit: true));
      await tester.tap(_outlineStar.first); // overall = 1
      await tester.pumpAndSettle();
      await tester.tap(find.text('Submit rating'));
      await tester.pumpAndSettle();
      expect(find.text('Could not submit. Try again.'), findsOneWidget);
    });

    testWidgets(
        'a session rating with an unanswered required question cannot be saved',
        (tester) async {
      // The end-of-session form (code=Session, with a target) carries
      // per-element questions; when one is required, the client must block the
      // submit until it is scored — so the rating is never sent/saved.
      final repo = _FakeFeedbackRepo(session: true, requiredQuestion: true);
      await _pump(
        tester,
        repo,
        rateScreen: const RateScreen(code: 'Session', targetId: 'sess-1'),
      );
      // The Session form rendered its required per-element question…
      expect(find.text('Speaker'), findsOneWidget);
      // Pick the overall stars so the no-stars guard passes first…
      await tester.tap(_outlineStar.first); // overall = 1
      await tester.pumpAndSettle();
      // …but leave the required "Speaker" question unscored, then submit.
      await tester.tap(find.text('Submit rating'));
      await tester.pumpAndSettle();
      // The save is blocked client-side and nothing is sent to the repository.
      expect(find.text('Please answer all required questions'), findsOneWidget);
      expect(repo.lastOverall, isNull);
      expect(find.text('Thanks for your rating'), findsNothing);
    });
  });
}
