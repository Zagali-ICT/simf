import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/session_lifecycle.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/session_state_chip.dart';

List<SessionChipKind> _chips({
  required SessionPhase phase,
  bool hasSummary = false,
  SessionStatus status = SessionStatus.scheduled,
}) =>
    sessionStateChips(
      phase: phase,
      hasPublishedSummary: hasSummary,
      status: status,
    );

void main() {
  group('sessionStateChips', () {
    test('a live session shows only the live chip (even with a summary)', () {
      expect(
        _chips(phase: SessionPhase.live, hasSummary: true),
        <SessionChipKind>[SessionChipKind.live],
      );
    });

    test('an ended session with a published summary shows summary-ready', () {
      expect(
        _chips(
          phase: SessionPhase.ended,
          hasSummary: true,
          status: SessionStatus.published,
        ),
        <SessionChipKind>[SessionChipKind.summaryReady],
      );
    });

    test('an ended recorded/published session with no summary shows recorded',
        () {
      expect(
        _chips(phase: SessionPhase.ended, status: SessionStatus.recorded),
        <SessionChipKind>[SessionChipKind.recorded],
      );
      expect(
        _chips(phase: SessionPhase.ended, status: SessionStatus.published),
        <SessionChipKind>[SessionChipKind.recorded],
      );
    });

    test('an ended plain (held/scheduled) session shows no chip', () {
      expect(_chips(phase: SessionPhase.ended, status: SessionStatus.held),
          isEmpty,);
      expect(
        _chips(phase: SessionPhase.ended),
        isEmpty,
      );
    });

    test('an upcoming session shows no chip', () {
      expect(
        _chips(phase: SessionPhase.upcoming, status: SessionStatus.recorded),
        isEmpty,
      );
    });
  });
}
