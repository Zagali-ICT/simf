import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/account/data/sign_in_validators.dart';

void main() {
  const en = AppL10n(Locale('en'));

  group('email', () {
    test('is required', () {
      expect(validateSignInEmail('', en), en.requiredField);
      expect(validateSignInEmail('   ', en), en.requiredField);
    });

    test('must look like an address', () {
      expect(validateSignInEmail('not-an-email', en), en.invalidEmail);
    });

    test('accepts a well-formed address, and tolerates surrounding space', () {
      expect(validateSignInEmail('visitor@example.com', en), isNull);
      expect(validateSignInEmail('  visitor@example.com  ', en), isNull);
    });
  });

  group('password', () {
    test('is required', () {
      expect(validateSignInPassword('', en), en.requiredField);
    });

    // Load-bearing difference from sign-up: an account created before a policy
    // change must still be able to sign in, and the SERVER authenticates the
    // value. Tightening this to the sign-up rules would lock out existing
    // users, so this test exists to stop that "consistency" fix.
    test('does NOT apply the sign-up password policy', () {
      for (final weak in <String>['a', 'password', '12345678']) {
        expect(validateSignInPassword(weak, en), isNull, reason: weak);
      }
    });
  });
}
