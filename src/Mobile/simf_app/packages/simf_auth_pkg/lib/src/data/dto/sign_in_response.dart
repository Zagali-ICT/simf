import 'package:meta/meta.dart';

import 'token_payload_dto.dart';

/// Wire shape for the `data` of `POST /app/auth/sign-in` — the backend
/// `SignInResponse` (`mfaRequired` / `mfaToken` / `otpToken` / `tokens` /
/// `accountState` / `passwordChangeToken`).
///
/// The app is **visitor-only**, so the only second factor is the **email OTP**
/// (`otpToken`) branch — there is no TOTP path in the app. On a non-2FA result
/// the tokens are nested under `tokens` (the server issues them for approved
/// **and** pending/rejected accounts alike — the registration status is read
/// separately from `GET /app/users/me`, not from this response).
@immutable
sealed class SignInResponseData {
  const SignInResponseData();

  factory SignInResponseData.fromJson(Map<String, dynamic> json) {
    if (json['mfaRequired'] == true) {
      // Visitor second factor = emailed OTP (otpToken). The admin TOTP
      // (mfaToken) path does not apply to the app.
      return OtpChallengeResponseData(
        otpToken: json['otpToken'] as String? ?? '',
      );
    }
    final tokens = json['tokens'];
    if (tokens is Map<String, dynamic>) {
      return TokenResponseData(payload: TokenPayloadDto.fromJson(tokens));
    }
    throw const FormatException(
      'Sign-in response carried neither tokens nor an OTP challenge.',
    );
  }
}

/// Sign-in completed: the issued token payload.
class TokenResponseData extends SignInResponseData {
  const TokenResponseData({required this.payload});
  final TokenPayloadDto payload;
}

/// Sign-in needs the emailed OTP second factor; [otpToken] is fed to
/// `POST /app/auth/verify-otp` with the code.
class OtpChallengeResponseData extends SignInResponseData {
  const OtpChallengeResponseData({required this.otpToken});
  final String otpToken;
}
