import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/widgets/session_filter_tabs.dart';

Future<void> _pump(
  WidgetTester tester, {
  required List<String> labels,
  bool equalWidth = false,
  int selected = 0,
}) async {
  var tapped = -1;
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: Directionality(
          textDirection: TextDirection.rtl,
          // A narrow phone width so a many-tab row would overflow if it didn't
          // scroll / wasn't capped.
          child: Center(
            child: SizedBox(
              width: 360,
              child: SessionFilterTabs(
                labels: labels,
                selectedIndex: selected,
                equalWidth: equalWidth,
                onSelected: (i) => tapped = i,
              ),
            ),
          ),
        ),
      ),
    ),
  );
  // Surface the closure so the analyzer keeps it (and a future tap test can use).
  expect(tapped, -1);
}

void main() {
  group('SessionFilterTabs', () {
    testWidgets('equal-width mode lays out 3 tabs with no overflow',
        (tester) async {
      await _pump(
        tester,
        labels: <String>['الجميع', 'جلساتي', 'المفضلة'],
        equalWidth: true,
      );
      expect(tester.takeException(), isNull);
      expect(find.text('الجميع'), findsOneWidget);
      expect(find.text('المفضلة'), findsOneWidget);
    });

    testWidgets('scrollable mode shows many long tabs without overflowing',
        (tester) async {
      // Presentations passes one tab per event day plus "All" — 6 here.
      await _pump(
        tester,
        labels: <String>[
          'الجميع',
          'اليوم الأول',
          'اليوم الثاني',
          'اليوم الثالث',
          'اليوم الرابع',
          'اليوم الخامس',
        ],
      );
      // No RenderFlex overflow was thrown laying these out at 360px.
      expect(tester.takeException(), isNull);
      expect(find.byType(SingleChildScrollView), findsOneWidget);
      expect(find.text('الجميع'), findsOneWidget);
    });

    testWidgets('a long English label does not overflow in scrollable mode',
        (tester) async {
      await _pump(
        tester,
        labels: <String>['Upcoming', 'Attended', 'Missed', 'Archive'],
      );
      expect(tester.takeException(), isNull);
      expect(find.text('Upcoming'), findsOneWidget);
    });
  });
}
