import 'package:meta/meta.dart';

import 'token_payload_dto.dart';

/// Wire shape for the `data` field of `POST /auth/sign-in` (SIMF-API-001
/// §12.4).
///
/// Two cases the response carries:
///   1. Standard user — full [TokenPayloadDto].
///   2. Administrative user requiring TOTP — `mfaRequired: true` plus an
///      `mfaToken` to feed into `/auth/verify-totp`.
@immutable
sealed class SignInResponseData {
  const SignInResponseData();

  factory SignInResponseData.fromJson(Map<String, dynamic> json) {
    if (json['mfaRequired'] == true) {
      return MfaChallengeResponseData(
        mfaToken: json['mfaToken'] as String? ?? '',
      );
    }
    return TokenResponseData(payload: TokenPayloadDto.fromJson(json));
  }
}

class TokenResponseData extends SignInResponseData {
  const TokenResponseData({required this.payload});
  final TokenPayloadDto payload;
}

class MfaChallengeResponseData extends SignInResponseData {
  const MfaChallengeResponseData({required this.mfaToken});
  final String mfaToken;
}
