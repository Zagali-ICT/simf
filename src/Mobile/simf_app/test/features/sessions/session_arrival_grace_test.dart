import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/sessions/data/session_detail_eligibility.dart';
import 'package:simf_app/features/sessions/data/session_enums.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/session_speaker.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Pins the SIGN of [showArrivalStatus]'s time gate: the check-in strip opens
/// one arrival-grace BEFORE the start, mirroring the server. Every case
/// straddles that boundary by less than one grace, so a flipped sign fails.
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
      // Inside the window that opens at start-60; start+60 would be 90 minutes
      // out.
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
      // Ten minutes in, grace 60: the window opened 70 minutes ago, and would
      // still be 50 minutes out with the sign flipped.
      final detail = _detail(
        start: saudiNow().subtract(const Duration(minutes: 10)),
        graceMinutes: 60,
      );

      expect(showArrivalStatus(detail, AppRole.visitor), isTrue);
    });

    test('a session further out than its grace is NOT offered the strip', () {
      // The negative control for the two cases above.
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
      // D-840 made the grace per-hall / per-session.
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
