import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/widgets/simf_search_field.dart';

/// Regression cover for **BUG-012** — form controls had no accessible name.
///
/// The shared search field draws its placeholder as a separate node that
/// vanishes the moment the user types, so the text field itself was unnamed and
/// a screen reader announced a bare "edit box" on every search surface
/// (speakers, booths, delegations, agenda, session summaries, notifications).
void main() {
  testWidgets('SimfSearchField exposes its hint as the field semantics label',
      (tester) async {
    final handle = tester.ensureSemantics();

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SimfSearchField(hint: 'ابحث عن متحدث', onChanged: (_) {}),
        ),
      ),
    );

    expect(
      tester.getSemantics(find.byType(SimfSearchField)),
      isSemantics(label: 'ابحث عن متحدث', isTextField: true),
    );

    handle.dispose();
  });

  testWidgets('the label survives once the field has text (the hint is gone)',
      (tester) async {
    final handle = tester.ensureSemantics();
    final controller = TextEditingController(text: 'الرياض');
    addTearDown(controller.dispose);

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SimfSearchField(
            hint: 'ابحث عن متحدث',
            controller: controller,
            onChanged: (_) {},
          ),
        ),
      ),
    );

    final node = tester.getSemantics(find.byType(SimfSearchField));
    expect(node.label, contains('ابحث عن متحدث'));

    handle.dispose();
  });
}
