import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/widgets/simf_forward_chevron.dart';

void main() {
  // The chevron asset points LEFT. SimfForwardChevron mirrors it in LTR only
  // (caret forward = right); in RTL it stays unmirrored (forward = left).
  Future<Transform> pumpChevron(WidgetTester tester, TextDirection dir) async {
    await tester.pumpWidget(
      Directionality(
        textDirection: dir,
        child: const SimfForwardChevron(
          'assets/icons/ic_back.svg',
          size: 20,
          color: Color(0xFFFFFFFF),
        ),
      ),
    );
    return tester.widget<Transform>(
      find
          .descendant(
            of: find.byType(SimfForwardChevron),
            matching: find.byType(Transform),
          )
          .first,
    );
  }

  testWidgets('flips the caret in LTR (forward points right)',
      (tester) async {
    final transform = await pumpChevron(tester, TextDirection.ltr);
    // Transform.flip(flipX: true) scales the X axis by -1.
    expect(transform.transform.entry(0, 0), -1.0);
  });

  testWidgets('keeps the caret unflipped in RTL (forward points left)',
      (tester) async {
    final transform = await pumpChevron(tester, TextDirection.rtl);
    expect(transform.transform.entry(0, 0), 1.0);
  });
}
