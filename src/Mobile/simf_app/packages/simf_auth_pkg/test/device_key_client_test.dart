import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:pointycastle/export.dart';
import 'package:simf_auth_pkg/src/data/device_key_client.dart';

BigInt _bytesToBigInt(List<int> bytes) {
  var result = BigInt.zero;
  for (final b in bytes) {
    result = (result << 8) | BigInt.from(b & 0xff);
  }
  return result;
}

void main() {
  group('DeviceKeyClient (ES256 / P-256, D-172)', () {
    test('SPKI is the 91-byte P-256 blob and the signature is 64 bytes', () {
      const client = DeviceKeyClient();
      final pair = client.generateKeyPair();

      final spki = base64.decode(pair.publicKeySpkiBase64);
      expect(spki.length, equals(91), reason: 'P-256 SubjectPublicKeyInfo');
      expect(spki[0], equals(0x30), reason: 'outer SEQUENCE');
      expect(
        spki[spki.length - 65],
        equals(0x04),
        reason: 'uncompressed EC point marker',
      );

      final challenge = base64.encode(
        Uint8List.fromList(List<int>.generate(32, (i) => i)),
      );
      final signature = base64.decode(
        client.sign(
          privateKeyBase64: pair.privateKeyBase64,
          challengeBase64: challenge,
        ),
      );
      expect(signature.length, equals(64), reason: 'IEEE-P1363 r||s');
    });

    test('the IEEE-P1363 signature verifies against the SPKI public key', () {
      const client = DeviceKeyClient();
      final pair = client.generateKeyPair();
      final challengeBytes =
          Uint8List.fromList(List<int>.generate(32, (i) => (i * 7) % 256));
      final challenge = base64.encode(challengeBytes);

      final sigBytes = base64.decode(
        client.sign(
          privateKeyBase64: pair.privateKeyBase64,
          challengeBase64: challenge,
        ),
      );

      // Reconstruct the public key from the SPKI point (0x04 || X || Y) and
      // verify the r||s signature — proves SPKI + signature are self-consistent.
      final spki = base64.decode(pair.publicKeySpkiBase64);
      final point = spki.sublist(spki.length - 65);
      final domain = ECCurve_secp256r1();
      final q = domain.curve.decodePoint(point)!;
      final pub = ECPublicKey(q, domain);
      final r = _bytesToBigInt(sigBytes.sublist(0, 32));
      final s = _bytesToBigInt(sigBytes.sublist(32, 64));

      final verifier = ECDSASigner(SHA256Digest())
        ..init(false, PublicKeyParameter<ECPublicKey>(pub));
      expect(
        verifier.verifySignature(challengeBytes, ECSignature(r, s)),
        isTrue,
      );
    });

    test('a signature does not verify against a different key', () {
      const client = DeviceKeyClient();
      final pair = client.generateKeyPair();
      final other = client.generateKeyPair();
      final challengeBytes =
          Uint8List.fromList(List<int>.generate(32, (i) => 255 - i));
      final challenge = base64.encode(challengeBytes);

      final sigBytes = base64.decode(
        client.sign(
          privateKeyBase64: pair.privateKeyBase64,
          challengeBase64: challenge,
        ),
      );
      final otherSpki = base64.decode(other.publicKeySpkiBase64);
      final domain = ECCurve_secp256r1();
      final q = domain.curve.decodePoint(otherSpki.sublist(otherSpki.length - 65))!;
      final verifier = ECDSASigner(SHA256Digest())
        ..init(false, PublicKeyParameter<ECPublicKey>(ECPublicKey(q, domain)));
      expect(
        verifier.verifySignature(
          challengeBytes,
          ECSignature(
            _bytesToBigInt(sigBytes.sublist(0, 32)),
            _bytesToBigInt(sigBytes.sublist(32, 64)),
          ),
        ),
        isFalse,
      );
    });
  });
}
