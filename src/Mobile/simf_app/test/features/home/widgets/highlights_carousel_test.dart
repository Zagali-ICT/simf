import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/home/widgets/carousel_dots.dart';
import 'package:simf_app/features/home/widgets/highlights_carousel.dart';
import 'package:simf_app/features/news/data/news_models.dart';

NewsListItem _post(String id) => NewsListItem(
      id: id,
      title: 'Post $id',
      titleArabic: 'خبر $id',
      category: 'News',
      categoryArabic: 'أخبار',
      publishedAt: DateTime.utc(2026, 8, 20),
    );

Widget _harness(List<NewsListItem> items) => MaterialApp(
      locale: const Locale('ar'),
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: Scaffold(
        body: Builder(
          builder: (context) => HighlightsCarousel(
            l10n: AppL10n.of(context),
            items: items,
            baseUrl: 'http://test.local/api/v1',
            onTap: (_) {},
          ),
        ),
      ),
    );

void main() {
  group('HighlightsCarousel (758:1239)', () {
    testWidgets('a single slide gets no dots and never auto-advances',
        (tester) async {
      await tester.pumpWidget(_harness(<NewsListItem>[_post('a')]));
      await tester.pump();
      expect(find.byType(CarouselDots), findsNothing);

      await tester.pump(const Duration(seconds: 4));
      await tester.pump(const Duration(milliseconds: 600));
      final pageView = tester.widget<PageView>(find.byType(PageView));
      expect(pageView.controller!.page, closeTo(0, 0.01));
    });

    testWidgets('growing past one slide starts the auto-advance',
        (tester) async {
      // A single highlight arrives first, so initState skips the timer.
      await tester.pumpWidget(_harness(<NewsListItem>[_post('a')]));
      await tester.pump();

      // A refresh returns more posts; the State is reused (no key), so only
      // didUpdateWidget can start the rotation. Regression guard for the
      // "carousel sits static" bug.
      await tester.pumpWidget(
        _harness(<NewsListItem>[_post('a'), _post('b'), _post('c')]),
      );
      await tester.pump();
      expect(find.byType(CarouselDots), findsOneWidget);

      await tester.pump(const Duration(seconds: 4));
      await tester.pump(const Duration(milliseconds: 600));
      final pageView = tester.widget<PageView>(find.byType(PageView));
      expect(pageView.controller!.page, closeTo(1, 0.01));
    });

    testWidgets('shrinking the list clamps the index back into range',
        (tester) async {
      await tester.pumpWidget(
        _harness(<NewsListItem>[_post('a'), _post('b'), _post('c')]),
      );
      await tester.pump();

      // Rotate to the last slide, then let a refresh drop the list to one post.
      await tester.pump(const Duration(seconds: 4));
      await tester.pump(const Duration(milliseconds: 600));
      await tester.pump(const Duration(seconds: 4));
      await tester.pump(const Duration(milliseconds: 600));
      expect(
        tester.widget<CarouselDots>(find.byType(CarouselDots)).index,
        2,
      );

      await tester.pumpWidget(_harness(<NewsListItem>[_post('a')]));
      await tester.pump();
      // The dots go with the second slide; the stale index must not survive
      // into the next growth, which would render a blank page.
      expect(find.byType(CarouselDots), findsNothing);

      await tester.pumpWidget(
        _harness(<NewsListItem>[_post('a'), _post('b')]),
      );
      await tester.pump();
      expect(
        tester.widget<CarouselDots>(find.byType(CarouselDots)).index,
        0,
      );
    });
  });
}
