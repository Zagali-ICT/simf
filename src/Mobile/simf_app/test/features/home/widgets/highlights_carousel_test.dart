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

      // The State is reused across the refresh (no key), so only
      // didUpdateWidget can start the rotation.
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

    testWidgets(
        'shrinking to two slides leaves the dots on the page the controller '
        'lands on', (tester) async {
      await tester.pumpWidget(
        _harness(<NewsListItem>[_post('a'), _post('b'), _post('c')]),
      );
      await tester.pump();

      // Rotate to the last slide, then let a refresh drop one post.
      await tester.pump(const Duration(seconds: 4));
      await tester.pump(const Duration(milliseconds: 600));
      await tester.pump(const Duration(seconds: 4));
      await tester.pump(const Duration(milliseconds: 600));
      expect(tester.widget<CarouselDots>(find.byType(CarouselDots)).index, 2);

      await tester.pumpWidget(
        _harness(<NewsListItem>[_post('a'), _post('b')]),
      );
      await tester.pump();

      // PageController settles a shrunken list on its LAST page, so the dots
      // have to follow it there.
      final pageView = tester.widget<PageView>(find.byType(PageView));
      expect(pageView.controller!.page, closeTo(1, 0.01));
      expect(tester.widget<CarouselDots>(find.byType(CarouselDots)).index, 1);

      // ...and the next tick still has somewhere to go: an index stuck at 0
      // targets the page already on screen and the carousel stops for good.
      await tester.pump(const Duration(seconds: 4));
      await tester.pump(const Duration(milliseconds: 600));
      expect(
        tester.widget<PageView>(find.byType(PageView)).controller!.page,
        closeTo(0, 0.01),
      );
    });

    testWidgets('shrinking mid-list never auto-advances backwards',
        (tester) async {
      final five = <NewsListItem>[
        _post('a'),
        _post('b'),
        _post('c'),
        _post('d'),
        _post('e'),
      ];
      await tester.pumpWidget(_harness(five));
      await tester.pump();
      for (var i = 0; i < 4; i++) {
        await tester.pump(const Duration(seconds: 4));
        await tester.pump(const Duration(milliseconds: 600));
      }
      expect(tester.widget<CarouselDots>(find.byType(CarouselDots)).index, 4);

      await tester.pumpWidget(_harness(five.take(3).toList()));
      await tester.pump();
      expect(tester.widget<CarouselDots>(find.byType(CarouselDots)).index, 2);

      // The controller sits on page 2, so the tick wraps forward to 0; from a
      // zeroed index it would target page 1 and slide BACKWARDS.
      await tester.pump(const Duration(seconds: 4));
      await tester.pump(const Duration(milliseconds: 600));
      expect(
        tester.widget<PageView>(find.byType(PageView)).controller!.page,
        closeTo(0, 0.01),
      );
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
      // The stale index must not survive into the next growth — it would
      // render a blank page.
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
