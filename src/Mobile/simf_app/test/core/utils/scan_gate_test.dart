import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/scan_gate.dart';

void main() {
  group('ScanGate', () {
    late DateTime now;
    late ScanGate gate;

    setUp(() {
      now = DateTime(2026, 7, 11, 12);
      gate = ScanGate(clock: () => now);
    });

    test('handles a code once, then blocks the same code while in view', () {
      expect(gate.shouldHandle('ABC'), isTrue);
      gate.markIdle();
      // Same code, still within the cooldown → blocked.
      expect(gate.shouldHandle('ABC'), isFalse);
    });

    test('blocks everything while busy (single-flight)', () {
      expect(gate.shouldHandle('ABC'), isTrue); // marks busy, no markIdle
      expect(gate.shouldHandle('XYZ'), isFalse);
      expect(gate.beginManual(), isFalse);
    });

    test('a different code fires even right after another', () {
      expect(gate.shouldHandle('ABC'), isTrue);
      gate.markIdle();
      expect(gate.shouldHandle('XYZ'), isTrue);
    });

    test('same code fires again after it leaves the frame (onNoCode)', () {
      expect(gate.shouldHandle('ABC'), isTrue);
      gate
        ..markIdle()
        ..onNoCode(); // the QR left the viewfinder
        expect(gate.shouldHandle('ABC'), isTrue);
    });

    test('same code fires again after the cooldown elapses', () {
      expect(gate.shouldHandle('ABC'), isTrue);
      gate.markIdle();
      expect(gate.shouldHandle('ABC'), isFalse);
      now = now.add(const Duration(seconds: 3)); // past the 2s cooldown
      expect(gate.shouldHandle('ABC'), isTrue);
    });

    test('empty code is never handled', () {
      expect(gate.shouldHandle(''), isFalse);
    });

    test('beginManual runs the same code even after a camera failure', () {
      expect(gate.shouldHandle('ABC'), isTrue);
      gate.markIdle(); // camera resolve failed
      // Manual retry of the same code bypasses the dedupe cooldown.
      expect(gate.beginManual(), isTrue);
    });
  });
}
