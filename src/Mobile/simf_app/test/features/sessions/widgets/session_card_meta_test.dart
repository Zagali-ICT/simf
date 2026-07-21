import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/widgets/session_card_meta.dart';

Future<void> _pump(WidgetTester tester, Widget child) =>
    tester.pumpWidget(MaterialApp(home: Scaffold(body: child)));

void main() {
  group('SessionIconLine', () {
    testWidgets('renders the Material glyph variant + the label',
        (tester) async {
      await _pump(
        tester,
        const SessionIconLine(icon: Icons.access_time, text: '09:00 · 45m'),
      );
      expect(find.byIcon(Icons.access_time), findsOneWidget);
      expect(find.text('09:00 · 45m'), findsOneWidget);
    });

    test('asserts exactly one of asset / icon', () {
      expect(() => SessionIconLine(text: 't'), throwsAssertionError);
      expect(
        () => SessionIconLine(text: 't', asset: 'a', icon: Icons.star),
        throwsAssertionError,
      );
    });
  });

  group('SessionMetaGroup', () {
    testWidgets('renders the glyph in the beige box + the label',
        (tester) async {
      await _pump(
        tester,
        const SessionMetaGroup(icon: Icons.person_outline, text: 'Dr Reef'),
      );
      expect(find.byIcon(Icons.person_outline), findsOneWidget);
      expect(find.text('Dr Reef'), findsOneWidget);
    });

    test('asserts exactly one of asset / icon', () {
      expect(() => SessionMetaGroup(text: 't'), throwsAssertionError);
    });
  });
}
