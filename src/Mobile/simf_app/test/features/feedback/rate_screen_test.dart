import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/feedback/rate_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class _FakeFeedbackRepo implements FeedbackRepository {
  _FakeFeedbackRepo({this.fail = false});

  final bool fail;
  int? lastStars;

  @override
  Future<void> submitRating({required int stars, String? comment}) async {
    lastStars = stars;
    if (fail) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
  }
}

Future<void> _pump(WidgetTester tester, FeedbackRepository repo) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[feedbackRepositoryProvider.overrideWithValue(repo)],
      child: MaterialApp(
        locale: const Locale('en'),
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
}

void main() {
  group('RateScreen (Page 040)', () {
    testWidgets('submitting with no stars prompts for a rating', (tester) async {
      final repo = _FakeFeedbackRepo();
      await _pump(tester, repo);
      await tester.tap(find.widgetWithText(FilledButton, 'Submit rating'));
      await tester.pumpAndSettle();
      expect(find.text('Please pick a star rating'), findsOneWidget);
      expect(repo.lastStars, isNull);
    });

    testWidgets('picking stars + submit sends the rating', (tester) async {
      final repo = _FakeFeedbackRepo();
      await _pump(tester, repo);
      // Tap the 4th star.
      await tester.tap(find.byIcon(Icons.star_border).at(3));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Submit rating'));
      await tester.pumpAndSettle();
      expect(repo.lastStars, 4);
      expect(find.text('Thanks for your rating'), findsOneWidget);
    });

    testWidgets('a failure shows the error toast', (tester) async {
      await _pump(tester, _FakeFeedbackRepo(fail: true));
      await tester.tap(find.byIcon(Icons.star_border).at(0));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Submit rating'));
      await tester.pumpAndSettle();
      expect(find.text('Could not submit. Try again.'), findsOneWidget);
    });
  });
}
