import 'package:meta/meta.dart';

/// Wire shapes for the device-key (biometric) endpoints (backend D-172):
/// `POST /app/auth/device-keys`, `…/{id}/challenge`,
/// `POST /app/auth/sign-in-with-device-key`.

@immutable
class RegisterDeviceKeyRequest {
  const RegisterDeviceKeyRequest({
    required this.publicKey,
    required this.algorithm,
    required this.label,
  });

  /// base64 SubjectPublicKeyInfo DER.
  final String publicKey;
  final String algorithm; // "ES256"
  final String label;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'publicKey': publicKey,
        'algorithm': algorithm,
        'label': label,
      };
}

@immutable
class DeviceKeyEntryDto {
  const DeviceKeyEntryDto({required this.id, required this.label});

  factory DeviceKeyEntryDto.fromJson(Map<String, dynamic> json) {
    return DeviceKeyEntryDto(
      id: json['id'] as String? ?? '',
      label: json['label'] as String? ?? '',
    );
  }

  final String id;
  final String label;
}

@immutable
class DeviceKeyChallengeDto {
  const DeviceKeyChallengeDto({
    required this.challenge,
    required this.expiresInSeconds,
  });

  factory DeviceKeyChallengeDto.fromJson(Map<String, dynamic> json) {
    return DeviceKeyChallengeDto(
      challenge: json['challenge'] as String? ?? '',
      expiresInSeconds: (json['expiresInSeconds'] as num?)?.toInt() ?? 0,
    );
  }

  final String challenge;
  final int expiresInSeconds;
}

@immutable
class SignInWithDeviceKeyRequest {
  const SignInWithDeviceKeyRequest({
    required this.deviceKeyId,
    required this.challenge,
    required this.signature,
  });

  final String deviceKeyId;
  final String challenge;

  /// base64 IEEE-P1363 (r‖s) signature.
  final String signature;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'deviceKeyId': deviceKeyId,
        'challenge': challenge,
        'signature': signature,
      };
}
