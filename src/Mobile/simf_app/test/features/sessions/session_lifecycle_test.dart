import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/session_lifecycle.dart';

void main() {
  final start = DateTime.utc(2026, 7, 14, 9);
  final end = DateTime.utc(2026, 7, 14, 10);

  group('sessionPhase', () {
    test('before start is upcoming', () {
      expect(
        sessionPhase(start, end, start.subtract(const Duration(minutes: 1))),
        SessionPhase.upcoming,
      );
    });

    test('exactly at start is live (start-inclusive)', () {
      expect(sessionPhase(start, end, start), SessionPhase.live);
    });

    test('within the window is live', () {
      expect(
        sessionPhase(start, end, start.add(const Duration(minutes: 30))),
        SessionPhase.live,
      );
    });

    test('exactly at end is ended (end-exclusive)', () {
      expect(sessionPhase(start, end, end), SessionPhase.ended);
    });

    test('after end is ended', () {
      expect(
        sessionPhase(start, end, end.add(const Duration(minutes: 1))),
        SessionPhase.ended,
      );
    });

    test('a zero-length window resolves (at the instant it is ended)', () {
      expect(sessionPhase(start, start, start), SessionPhase.ended);
      expect(
        sessionPhase(start, start, start.subtract(const Duration(seconds: 1))),
        SessionPhase.upcoming,
      );
    });
  });
}
