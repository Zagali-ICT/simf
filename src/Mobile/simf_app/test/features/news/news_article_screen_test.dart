import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_app/features/news/data/news_repository.dart';
import 'package:simf_app/features/news/news_article_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

NewsArticle _article() => NewsArticle(
      id: 'n1',
      title: 'Forum opens',
      titleArabic: 'افتتاح الملتقى',
      body: 'The 4th edition begins today.',
      bodyArabic: 'تبدأ النسخة الرابعة اليوم.',
      category: 'Press',
      categoryArabic: 'صحافة',
      publishedAt: DateTime.utc(2026, 11, 23),
    );

class _FakeNewsRepo implements NewsRepository {
  _FakeNewsRepo({this.article, this.status});

  final NewsArticle? article;
  final int? status;
  int calls = 0;

  @override
  Future<NewsArticle> getArticle(String id) async {
    calls++;
    if (status != null) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return article!;
  }
}

Future<void> _pump(WidgetTester tester, NewsRepository repo) async {
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[newsRepositoryProvider.overrideWithValue(repo)],
      child: const MaterialApp(
        locale: Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: NewsArticleScreen(newsId: 'n1'),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('NewsArticleScreen (Page 029 detail)', () {
    testWidgets('renders the article body', (tester) async {
      await _pump(tester, _FakeNewsRepo(article: _article()));
      expect(find.text('Forum opens'), findsOneWidget);
      expect(find.text('Press'), findsOneWidget);
      expect(find.text('The 4th edition begins today.'), findsOneWidget);
    });

    testWidgets('a 404 shows the not-found state', (tester) async {
      await _pump(tester, _FakeNewsRepo(status: 404));
      expect(find.text('This article was not found'), findsOneWidget);
    });

    testWidgets('a non-404 error shows retry, which re-fetches',
        (tester) async {
      final repo = _FakeNewsRepo(status: 500);
      await _pump(tester, repo);
      expect(find.text('Could not load the news.'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Retry'));
      await tester.pumpAndSettle();
      expect(repo.calls, greaterThanOrEqualTo(2));
    });
  });
}
