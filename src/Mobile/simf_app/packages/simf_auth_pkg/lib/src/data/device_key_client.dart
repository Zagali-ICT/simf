import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:meta/meta.dart';
import 'package:pointycastle/export.dart';

/// A freshly-generated device key: the private scalar (kept on-device, in
/// secure storage) and the public key as a base64 `SubjectPublicKeyInfo` DER
/// blob (sent to the server at registration).
@immutable
class DeviceKeyPair {
  const DeviceKeyPair({
    required this.privateKeyBase64,
    required this.publicKeySpkiBase64,
  });

  /// The 32-byte big-endian private scalar, base64. Stays in secure storage.
  final String privateKeyBase64;

  /// The base64 `SubjectPublicKeyInfo` DER (importable by
  /// `ECDsa.ImportSubjectPublicKeyInfo` on the server).
  final String publicKeySpkiBase64;
}

/// ES256 (ECDSA P-256) device-key crypto for biometric sign-in (D-172).
///
/// Produces exactly what the backend `DeviceKeyService` verifies: a base64
/// `SubjectPublicKeyInfo` public key and an IEEE-P1363 raw `r‖s` (64-byte)
/// signature over the challenge bytes, base64-encoded. The private scalar never
/// leaves the device.
///
/// NOTE: the .NET ↔ Dart byte-interop (SPKI import + `VerifyData` with
/// `IeeeP1363FixedFieldConcatenation`) is to be integration-verified against the
/// running backend (simf-run); the round-trip is unit-tested within Dart here.
/// A future hardening step (D-738 Tier-2) moves the key into the platform
/// secure enclave — Android Keystore/StrongBox with setUserAuthenticationRequired
/// + iOS Secure Enclave — so it is biometric-bound and non-exportable; the
/// backend contract (SPKI + ES256) is unchanged. See Page_003 Logic L-2.
class DeviceKeyClient {
  const DeviceKeyClient();

  /// The only algorithm the backend accepts (DeviceKeyService).
  static const String algorithm = 'ES256';

  /// The fixed P-256 `SubjectPublicKeyInfo` DER prefix — everything up to and
  /// including the BIT STRING header; the 65-byte uncompressed EC point
  /// (`0x04 ‖ X ‖ Y`) is appended. (SEQUENCE { AlgorithmIdentifier {
  /// id-ecPublicKey, prime256v1 }, BIT STRING }.)
  static const List<int> _spkiPrefix = <int>[
    0x30, 0x59, 0x30, 0x13, 0x06, 0x07, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x02,
    0x01, 0x06, 0x08, 0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x03, 0x01, 0x07, 0x03,
    0x42, 0x00,
  ];

  /// Generates a new P-256 key pair.
  DeviceKeyPair generateKeyPair() {
    final ECDomainParameters domain = ECCurve_secp256r1();
    final generator = ECKeyGenerator()
      ..init(
        ParametersWithRandom(
          ECKeyGeneratorParameters(domain),
          _secureRandom(),
        ),
      );
    final pair = generator.generateKeyPair();
    final priv = pair.privateKey as ECPrivateKey;
    final pub = pair.publicKey as ECPublicKey;
    return DeviceKeyPair(
      privateKeyBase64: base64.encode(_bigIntTo32(priv.d!)),
      publicKeySpkiBase64: base64.encode(_encodeSpki(pub)),
    );
  }

  /// Signs the base64 [challengeBase64] (decoded to bytes) with the private key
  /// and returns the IEEE-P1363 `r‖s` signature, base64.
  String sign({
    required String privateKeyBase64,
    required String challengeBase64,
  }) {
    final ECDomainParameters domain = ECCurve_secp256r1();
    final d = _bytesToBigInt(base64.decode(privateKeyBase64));
    final priv = ECPrivateKey(d, domain);
    final signer = ECDSASigner(SHA256Digest())
      ..init(
        true,
        ParametersWithRandom(
          PrivateKeyParameter<ECPrivateKey>(priv),
          _secureRandom(),
        ),
      );
    final signature =
        signer.generateSignature(base64.decode(challengeBase64)) as ECSignature;
    final out = Uint8List(64);
    out.setRange(0, 32, _bigIntTo32(signature.r));
    out.setRange(32, 64, _bigIntTo32(signature.s));
    return base64.encode(out);
  }

  Uint8List _encodeSpki(ECPublicKey pub) {
    final q = pub.Q!;
    final x = _bigIntTo32(q.x!.toBigInteger()!);
    final y = _bigIntTo32(q.y!.toBigInteger()!);
    final out = Uint8List(_spkiPrefix.length + 65);
    out.setRange(0, _spkiPrefix.length, _spkiPrefix);
    out[_spkiPrefix.length] = 0x04; // uncompressed point marker
    out.setRange(_spkiPrefix.length + 1, _spkiPrefix.length + 33, x);
    out.setRange(_spkiPrefix.length + 33, _spkiPrefix.length + 65, y);
    return out;
  }

  static SecureRandom _secureRandom() {
    final random = FortunaRandom();
    final seedSource = Random.secure();
    final seed = Uint8List(32);
    for (var i = 0; i < seed.length; i++) {
      seed[i] = seedSource.nextInt(256);
    }
    random.seed(KeyParameter(seed));
    return random;
  }

  /// Big-endian, left-padded to exactly 32 bytes (the P-256 field size).
  static Uint8List _bigIntTo32(BigInt value) {
    final bytes = Uint8List(32);
    var v = value;
    final mask = BigInt.from(0xff);
    for (var i = 31; i >= 0; i--) {
      bytes[i] = (v & mask).toInt();
      v = v >> 8;
    }
    return bytes;
  }

  static BigInt _bytesToBigInt(List<int> bytes) {
    var result = BigInt.zero;
    for (final b in bytes) {
      result = (result << 8) | BigInt.from(b & 0xff);
    }
    return result;
  }
}
