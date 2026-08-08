import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/session_detail_eligibility.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// These rules decide which actions a session detail OFFERS, so a wrong answer
/// either hides a legitimate action or dangles one the router will bounce. They
/// were getters inside a 500-line widget; as pure functions they can be asserted
/// per role, which is what the defect ids below were fixed for.
void main() {
  group('canJoinSession (DEF-MOD-004)', () {
    test('is offered to attendees', () {
      expect(canJoinSession(AppRole.visitor), isTrue);
    });

    test('is NOT offered to a guest', () {
      expect(canJoinSession(AppRole.guest), isFalse);
    });

    // The bug this rule fixed: an operational role saw a join affordance the
    // router then refused.
    test('is NOT offered to operational roles', () {
      for (final role in <AppRole>[
        AppRole.moderator,
        AppRole.staff,
      ]) {
        expect(canJoinSession(role), isFalse, reason: role.name);
      }
    });
  });

  group('canAskQuestion (DEF-MOD-003)', () {
    // A guest KEEPS the card - shown disabled, as the sign-in nudge.
    test('is offered to a guest, as the sign-in nudge', () {
      expect(canAskQuestion(AppRole.guest), isTrue);
    });

    test('is offered to attendees', () {
      expect(canAskQuestion(AppRole.visitor), isTrue);
    });

    test('is NOT offered to a role the router would bounce', () {
      expect(canAskQuestion(AppRole.staff), isFalse);
    });
  });

  group('roleOf', () {
    test('a signed-out session is a guest', () {
      expect(roleOf(const AuthStateSignedOut()), AppRole.guest);
    });
  });
}
