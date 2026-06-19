import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/feedback/rate_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _FakeFeedbackRepo implements FeedbackRepository {
  _FakeFeedbackRepo({this.fail = false});

  final bool fail;
  int? lastStars;
  int? lastOrganization;
  int? lastContent;
  int? lastApp;
  int? lastVenue;

  @override
  Future<void> submitRating({
    required int stars,
    String? comment,
    int? organizationStars,
    int? contentStars,
    int? appStars,
    int? venueStars,
  }) async {
    lastStars = stars;
    lastOrganization = organizationStars;
    lastContent = contentStars;
    lastApp = appStars;
    lastVenue = venueStars;
    if (fail) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
  }
}

Future<void> _pump(WidgetTester tester, FeedbackRepository repo) async {
  // A tall surface so the whole scroll content lays out under the lazy ListView.
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

/// The outline (empty) star icon used by the bars (frame 1116:16894).
final _outlineStar = find.byIcon(Icons.star_outline_rounded);

/// The star bar inside the per-element row carrying [label].
Finder _categoryStars(String label) => find.descendant(
      of: find.ancestor(of: find.text(label), matching: find.byType(Row)).first,
      matching: find.byIcon(Icons.star_outline_rounded),
    );

void main() {
  group('RateScreen (Page 040 — frame 1116:16894)', () {
    testWidgets('submitting with no stars prompts for a rating', (tester) async {
      final repo = _FakeFeedbackRepo();
      await _pump(tester, repo);
      await tester.tap(find.text('Submit rating'));
      await tester.pumpAndSettle();
      expect(find.text('Please pick a star rating'), findsOneWidget);
      expect(repo.lastStars, isNull);
    });

    testWidgets('picking the overall stars + submit sends the rating',
        (tester) async {
      final repo = _FakeFeedbackRepo();
      await _pump(tester, repo);
      // The overall star bar is the first in the tree → its 4th star = 4.
      await tester.tap(_outlineStar.at(3));
      await tester.pumpAndSettle();
      // The dynamic summary appears for the chosen score.
      expect(find.text('4 of 5 · Very good'), findsOneWidget);
      await tester.tap(find.text('Submit rating'));
      await tester.pumpAndSettle();
      expect(repo.lastStars, 4);
      // Untouched element scores ride null.
      expect(repo.lastOrganization, isNull);
      expect(find.text('Thanks for your rating'), findsOneWidget);
    });

    testWidgets('a per-element score is sent alongside the overall stars',
        (tester) async {
      final repo = _FakeFeedbackRepo();
      await _pump(tester, repo);
      await tester.tap(_outlineStar.at(2)); // overall = 3
      await tester.pumpAndSettle();
      await tester.tap(_categoryStars('Organization').at(4)); // organization = 5
      await tester.pumpAndSettle();
      await tester.tap(find.text('Submit rating'));
      await tester.pumpAndSettle();
      expect(repo.lastStars, 3);
      expect(repo.lastOrganization, 5);
    });

    testWidgets('a failure shows the error toast', (tester) async {
      await _pump(tester, _FakeFeedbackRepo(fail: true));
      await tester.tap(_outlineStar.first); // overall = 1
      await tester.pumpAndSettle();
      await tester.tap(find.text('Submit rating'));
      await tester.pumpAndSettle();
      expect(find.text('Could not submit. Try again.'), findsOneWidget);
    });
  });
}
