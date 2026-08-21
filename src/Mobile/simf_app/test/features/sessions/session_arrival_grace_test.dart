import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/sessions/data/session_detail_eligibility.dart';
import 'package:simf_app/features/sessions/data/session_enums.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/session_speaker.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// [showArrivalStatus]'s time gate, pinned on the side that matters.
///
/// The window OPENS EARLY: the check-in strip appears one arrival-grace
/// BEFORE the session starts, mirroring the server, so an attendee walking in
/// ahead of time can scan. Flip that subtraction to an addition and the strip
/// only appears one grace AFTER the start — the attendee who arrives on time
/// is shown nothing and the gate refuses them. Nothing in the suite noticed,
/// because every existing case sat far enough inside the window that a
/// ±grace shift did not change the answer.
///
/// The cases below straddle the boundary by less than one grace in each
/// direction, so the sign of the shift decides the result. No clock trap here:
/// the offset under test is measured in minutes off the session's own start
/// and both spellings read the same [saudiNow].
SessionDetail _detail({
  required DateTime start,
  required int graceMinutes,
  SessionType? type,
}) =>
    SessionDetail(
      id: 's1',
      code: 'OP-1',
      title: 'Opening',
      titleArabic: 'الافتتاح',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      start: start,
      end: start.add(const Duration(hours: 1)),
      speakers: const <SessionSpeaker>[],
      type: type,
      arrivalGraceMinutes: graceMinutes,
    );

void main() {
  group('showArrivalStatus — the arrival window opens BEFORE the start', () {
    test('an attendee inside the pre-start grace is offered the strip', () {
      // 30 minutes before a start with a 60-minute grace: inside the window
      // that opens at start-60. Add the grace instead and the window would not
      // open until start+60, i.e. 90 minutes from now — the attendee arriving
      // early is refused.
      final detail = _detail(
        start: saudiNow().add(const Duration(minutes: 30)),
        graceMinutes: 60,
      );

      expect(
        showArrivalStatus(detail, AppRole.visitor),
        isTrue,
        reason: 'The check-in strip must be offered one arrival grace BEFORE '
            'the start. Hidden here means the window opened late and an '
            'on-time attendee has no way to report their arrival.',
      );
    });

    test('an attendee who arrived just after the start is offered the strip',
        () {
      // Ten minutes into the session, grace 60. Correct: the window opened 70
      // minutes ago. Adding the grace pushes the opening to 50 minutes from
      // now, so a visitor already in the hall is told nothing.
      final detail = _detail(
        start: saudiNow().subtract(const Duration(minutes: 10)),
        graceMinutes: 60,
      );

      expect(showArrivalStatus(detail, AppRole.visitor), isTrue);
    });

    test('a session further out than its grace is NOT offered the strip', () {
      // The negative control: without it the two cases above would also pass
      // against a gate that simply always says yes.
      final detail = _detail(
        start: saudiNow().add(const Duration(hours: 5)),
        graceMinutes: 15,
      );

      expect(
        showArrivalStatus(detail, AppRole.visitor),
        isFalse,
        reason: 'A session five hours out has no arrival to report yet.',
      );
    });

    test('the boundary is the session OWN grace, not a fixed 15 minutes', () {
      // D-840 made the grace per-hall / per-session. A 5-minute grace must
      // still be closed 30 minutes out; the 60-minute case above must be open.
      final tight = _detail(
        start: saudiNow().add(const Duration(minutes: 30)),
        graceMinutes: 5,
      );

      expect(showArrivalStatus(tight, AppRole.visitor), isFalse);
    });

    test('a workshop never shows it, however close its start', () {
      final workshop = _detail(
        start: saudiNow().add(const Duration(minutes: 1)),
        graceMinutes: 60,
        type: SessionType.workshop,
      );

      expect(showArrivalStatus(workshop, AppRole.visitor), isFalse);
    });

    test('a guest is never offered it, however close the start', () {
      final detail = _detail(
        start: saudiNow().add(const Duration(minutes: 1)),
        graceMinutes: 60,
      );

      expect(showArrivalStatus(detail, AppRole.guest), isFalse);
    });
  });
}
