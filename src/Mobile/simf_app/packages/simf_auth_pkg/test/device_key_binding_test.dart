import 'package:flutter_test/flutter_test.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

void main() {
  group('DeviceKeyBinding', () {
    test('the digest is stable for the same key and address', () {
      final a = DeviceKeyBinding.digestFor(
        deviceKeyId: 'dk-1',
        email: 'visitor@example.sa',
      );
      final b = DeviceKeyBinding.digestFor(
        deviceKeyId: 'dk-1',
        email: 'visitor@example.sa',
      );
      expect(a, equals(b));
      // sha256 hex.
      expect(a, matches(RegExp(r'^[0-9a-f]{64}$')));
    });

    test('a different address gives a different digest', () {
      expect(
        DeviceKeyBinding.digestFor(
          deviceKeyId: 'dk-1',
          email: 'visitor@example.sa',
        ),
        isNot(
          DeviceKeyBinding.digestFor(
            deviceKeyId: 'dk-1',
            email: 'someone.else@example.sa',
          ),
        ),
      );
    });

    test('the SAME address on a different key gives a different digest', () {
      // The key id is the salt, so one person's address cannot be correlated
      // across two installs by comparing what is stored on each.
      expect(
        DeviceKeyBinding.digestFor(
          deviceKeyId: 'dk-1',
          email: 'visitor@example.sa',
        ),
        isNot(
          DeviceKeyBinding.digestFor(
            deviceKeyId: 'dk-2',
            email: 'visitor@example.sa',
          ),
        ),
      );
    });

    test('matching is case- and whitespace-insensitive', () {
      final binding = DeviceKeyBinding.create(
        userId: 'u1',
        deviceKeyId: 'dk-1',
        email: 'Visitor@Example.SA',
      );
      // A reader's capitalisation must never lock them out of their own
      // credential, and a keyboard's trailing space is not a different person.
      for (final typed in <String>[
        'visitor@example.sa',
        'VISITOR@EXAMPLE.SA',
        '  visitor@example.sa  ',
      ]) {
        expect(
          binding.matchesEmail(deviceKeyId: 'dk-1', email: typed),
          isTrue,
          reason: typed,
        );
      }
      expect(
        binding.matchesEmail(deviceKeyId: 'dk-1', email: 'other@example.sa'),
        isFalse,
      );
    });

    test('the stored address is a digest, never the address itself', () {
      // This value is read on the sign-in screen, before anything is
      // authenticated, by whoever holds the phone.
      final binding = DeviceKeyBinding.create(
        userId: 'u1',
        deviceKeyId: 'dk-1',
        email: 'visitor@example.sa',
      );
      expect(binding.emailDigest, isNot(contains('visitor')));
      expect(binding.toJson().toString(), isNot(contains('visitor@')));
    });

    test('masking matches the server EmailMask shape', () {
      expect(DeviceKeyBinding.mask('visitor@example.sa'), 'v***@example.sa');
      expect(DeviceKeyBinding.mask('a@b.com'), 'a***@b.com');
      // Degenerate input must not throw or leak a half-formed address.
      expect(DeviceKeyBinding.mask('@example.sa'), '***');
      expect(DeviceKeyBinding.mask('no-at-sign'), '***');
      expect(DeviceKeyBinding.mask(''), '***');
    });

    test('json round-trips', () {
      final binding = DeviceKeyBinding.create(
        userId: 'u1',
        deviceKeyId: 'dk-1',
        email: 'visitor@example.sa',
      );
      final back = DeviceKeyBinding.fromJson(binding.toJson());
      expect(back.userId, binding.userId);
      expect(back.emailDigest, binding.emailDigest);
      expect(back.maskedEmail, binding.maskedEmail);
    });
  });
}
