import 'package:flutter_test/flutter_test.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

// Lives in the app suite (not the auth package's own test/) so it compiles
// against the app's resolved dependency set — running the auth package in
// isolation currently fails on an unrelated dio-version switch in simf_data_pkg.

CurrentUser _user(AppRole role, RegistrationStatus status) => CurrentUser(
      id: 'u1',
      email: 'visitor@example.sa',
      displayName: 'Visitor',
      appRole: role,
      preferredLanguage: PreferredLanguage.fromJson('ar'),
      registrationStatus: status,
    );

void main() {
  group('CurrentUser.effectiveAppRole (D-666)', () {
    test('an approved account keeps its real role', () {
      expect(
        _user(AppRole.visitor, RegistrationStatus.approved).effectiveAppRole,
        AppRole.visitor,
      );
      expect(
        _user(AppRole.staff, RegistrationStatus.approved).effectiveAppRole,
        AppRole.staff,
      );
    });

    test('a pending account presents as guest, whatever role the token carries',
        () {
      expect(
        _user(AppRole.visitor, RegistrationStatus.pending).effectiveAppRole,
        AppRole.guest,
      );
      expect(
        _user(AppRole.exhibitor, RegistrationStatus.pending).effectiveAppRole,
        AppRole.guest,
      );
    });

    test('a rejected account also presents as guest', () {
      expect(
        _user(AppRole.visitor, RegistrationStatus.rejected).effectiveAppRole,
        AppRole.guest,
      );
    });

    test('isApproved is true only for the approved status', () {
      expect(
        _user(AppRole.visitor, RegistrationStatus.approved).isApproved,
        isTrue,
      );
      expect(
        _user(AppRole.visitor, RegistrationStatus.pending).isApproved,
        isFalse,
      );
      expect(
        _user(AppRole.visitor, RegistrationStatus.rejected).isApproved,
        isFalse,
      );
    });
  });
}
