import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_app/features/news/news_screen.dart';

final _items = <NewsListItem>[
  NewsListItem(
    id: 'n1',
    title: 'Forum opens',
    titleArabic: 'افتتاح الملتقى',
    category: 'Press',
    categoryArabic: 'صحافة',
    publishedAt: DateTime.utc(2026, 11, 23),
    excerpt: 'The 4th edition begins.',
  ),
];

Future<void> _pump(WidgetTester tester, List<Override> overrides) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: overrides,
      child: MaterialApp(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: const NewsScreen(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('NewsScreen (Page 029)', () {
    testWidgets('lists news with category + excerpt', (tester) async {
      await _pump(tester, <Override>[
        newsListProvider.overrideWith((ref) async => _items),
      ]);
      expect(find.text('Forum opens'), findsOneWidget);
      expect(find.text('Press'), findsOneWidget);
      expect(find.text('The 4th edition begins.'), findsOneWidget);
    });

    testWidgets('empty shows the empty state', (tester) async {
      await _pump(tester, <Override>[
        newsListProvider.overrideWith((ref) async => const <NewsListItem>[]),
      ]);
      expect(find.text('No news'), findsOneWidget);
    });

    testWidgets('error shows the error state', (tester) async {
      await _pump(tester, <Override>[
        newsListProvider.overrideWith((ref) async => throw Exception('x')),
      ]);
      expect(find.text('Could not load the news.'), findsOneWidget);
    });
  });

  group('NewsArticle.fromJson', () {
    test('binds title + body', () {
      final a = NewsArticle.fromJson(<String, dynamic>{
        'id': 'n1',
        'title': 'T',
        'titleArabic': 'ع',
        'body': 'Body',
        'bodyArabic': 'نص',
        'category': 'Press',
        'categoryArabic': 'صحافة',
        'publishedAt': '2026-11-23T00:00:00Z',
      });
      expect(a.localizedTitle(false), 'T');
      expect(a.localizedBody(true), 'نص');
    });
  });
}
