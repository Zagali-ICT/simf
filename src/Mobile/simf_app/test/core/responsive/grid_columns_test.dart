import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/responsive/grid_columns.dart';

/// The point of this helper is that a phone renders EXACTLY as before while a
/// tablet stops stretching tiles, so both halves are asserted: the compact
/// count is the frame's own number, and the wider classes add columns.
void main() {
  Future<int> columnsAt(WidgetTester tester, double width, int compact) async {
    late int result;
    await tester.binding.setSurfaceSize(Size(width, 900));
    await tester.pumpWidget(
      MediaQuery(
        data: MediaQueryData(size: Size(width, 900)),
        child: Builder(
          builder: (context) {
            result = responsiveGridColumns(context, compact: compact);
            return const SizedBox.shrink();
          },
        ),
      ),
    );
    return result;
  }

  testWidgets('a phone keeps the frame count exactly', (tester) async {
    // 375 is the width every Figma frame is drawn at, and the width every
    // golden renders at. Changing this would change the shipped phone layout.
    expect(await columnsAt(tester, 375, 2), 2);
    expect(await columnsAt(tester, 375, 3), 3);
    // Still compact just under the boundary.
    expect(await columnsAt(tester, 599, 2), 2);
  });

  testWidgets('a medium window gains one column', (tester) async {
    expect(await columnsAt(tester, 600, 2), 3);
    expect(await columnsAt(tester, 904, 3), 4);
  });

  testWidgets('expanded and large gain two', (tester) async {
    // The owner's tablet lands here. A 2-column gallery grid used to render its
    // 164px tile at 504px; four columns keep it near the designed size.
    expect(await columnsAt(tester, 1024, 2), 4);
    expect(await columnsAt(tester, 1280, 3), 5);
  });

  testWidgets('the count never drops below the frame count', (tester) async {
    for (final width in <double>[375, 600, 905, 1024, 1440]) {
      expect(
        await columnsAt(tester, width, 2),
        greaterThanOrEqualTo(2),
        reason: 'width $width',
      );
    }
  });
}
