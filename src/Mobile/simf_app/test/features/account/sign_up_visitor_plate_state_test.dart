import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/account/widgets/sign_up_visitor_plate_field.dart';

/// Unit cover for the plate holder extracted out of `sign_up_visitor_screen`
/// (C6 — D-459/D-468/D-471). The screen's own widget tests drive the pickers;
/// these pin the assembly rules the holder now owns, which are the part with no
/// UI of its own.
void main() {
  group('SignUpVisitorPlateState', () {
    test('routes each letter pick to its own slot and re-assembles', () {
      final state = SignUpVisitorPlateState();
      addTearDown(state.dispose);

      state.digits.text = '1234';
      state
        ..setLetter(1, 'A')
        ..setLetter(2, 'B')
        ..setLetter(3, 'J');

      expect(state.letter1, 'A');
      expect(state.letter2, 'B');
      expect(state.letter3, 'J');
      expect(state.value, 'ABJ1234');
    });

    test('re-picking one position leaves the other two alone', () {
      final state = SignUpVisitorPlateState()
        ..setLetter(1, 'A')
        ..setLetter(2, 'B')
        ..setLetter(3, 'J');
      addTearDown(state.dispose);

      state.setLetter(2, 'D');

      expect(state.letter1, 'A');
      expect(state.letter2, 'D');
      expect(state.letter3, 'J');
    });

    test('a digits-first stored plate round-trips in its stored order (D-471)',
        () {
      final state = SignUpVisitorPlateState()..setFromCode('1234ABJ');
      addTearDown(state.dispose);

      expect(state.digitsFirst, isTrue);
      expect(state.digits.text, '1234');
      expect(state.value, '1234ABJ');
    });

    test('a code the pickers cannot represent is kept verbatim (D-468)', () {
      // Separators are dropped by the canonicaliser, but the value itself is
      // kept rather than erased by an unrelated profile edit.
      final state = SignUpVisitorPlateState()..setFromCode('LEGACY-PLATE-9');
      addTearDown(state.dispose);

      expect(state.value, 'LEGACYPLATE9');
    });

    test('an empty pick/digit state assembles to empty — the plate is optional',
        () {
      final state = SignUpVisitorPlateState()..sync();
      addTearDown(state.dispose);

      expect(state.value, '');
    });
  });
}
