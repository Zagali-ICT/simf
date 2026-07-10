import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'dto/badge_auth_dtos.dart';
import 'dto/current_user_dto.dart';
import 'dto/device_key_dtos.dart';
import 'dto/sign_in_request.dart';
import 'dto/sign_in_response.dart';
import 'dto/sign_up_request.dart';
import 'dto/token_payload_dto.dart';
import 'dto/verify_email_request.dart';

/// Thin layer over [SimfApiClient] that owns the auth endpoint paths.
///
/// Does not catch errors and does not map to domain types — that's the
/// repository's job. This class exists so the path strings are in one
/// file and the repository reads as method calls.
class AuthApi {
  AuthApi(this._client);

  final SimfApiClient _client;

  // SIMF-API-001 §12.4
  Future<SignInResponseData> signIn(SignInRequest request) {
    return _client.post<SignInResponseData>(
      '/app/auth/sign-in',
      body: request.toJson(),
      decodeData: (data) {
        if (data is! Map<String, dynamic>) {
          throw const FormatException(
            'sign-in response data was not an object.',
          );
        }
        return SignInResponseData.fromJson(data);
      },
    );
  }

  // D-738 — badge-QR password sign-in; returns the standard sign-in envelope.
  Future<SignInResponseData> badgeSignIn(BadgeSignInRequest request) {
    return _client.post<SignInResponseData>(
      '/app/auth/badge-sign-in',
      body: request.toJson(),
      decodeData: (data) {
        if (data is! Map<String, dynamic>) {
          throw const FormatException(
            'badge-sign-in response data was not an object.',
          );
        }
        return SignInResponseData.fromJson(data);
      },
    );
  }

  // SIMF-API-001 Amendment A.1 — visitor email-OTP second factor.
  Future<TokenPayloadDto> verifyOtp(VerifyOtpRequest request) {
    return _client.post<TokenPayloadDto>(
      '/app/auth/verify-otp',
      body: request.toJson(),
      decodeData: _decodeTokenPayload,
    );
  }

  // #12 — re-issue the emailed sign-in OTP in place (no re-authentication).
  Future<ResendOtpResponseDto> resendOtp(ResendOtpRequest request) {
    return _client.post<ResendOtpResponseDto>(
      '/app/auth/resend-otp',
      body: request.toJson(),
      decodeData: (data) {
        if (data is! Map<String, dynamic>) {
          throw const FormatException('resend-otp response was not an object.');
        }
        return ResendOtpResponseDto.fromJson(data);
      },
    );
  }

  // SIMF-API-001 §12.4
  Future<Map<String, dynamic>> signUp(SignUpRequest request) {
    return _client.post<Map<String, dynamic>>(
      '/app/auth/sign-up',
      body: request.toJson(),
      decodeData: (data) => (data as Map?)?.cast<String, dynamic>() ?? const {},
    );
  }

  Future<Map<String, dynamic>> verifyEmail(VerifyEmailRequest request) {
    return _client.post<Map<String, dynamic>>(
      '/app/auth/verify-email',
      body: request.toJson(),
      decodeData: (data) => (data as Map?)?.cast<String, dynamic>() ?? const {},
    );
  }

  Future<Map<String, dynamic>> resendCode(ResendCodeRequest request) {
    return _client.post<Map<String, dynamic>>(
      '/app/auth/resend-code',
      body: request.toJson(),
      decodeData: (data) => (data as Map?)?.cast<String, dynamic>() ?? const {},
    );
  }

  Future<TokenPayloadDto> refresh(RefreshRequest request) {
    return _client.post<TokenPayloadDto>(
      '/app/auth/refresh',
      body: request.toJson(),
      decodeData: _decodeTokenPayload,
      // The refresh call must not re-enter the 401 refresh path: a 401 here
      // (expired / reuse-detected refresh token) would otherwise deadlock the
      // single-flight refresh against itself.
      skipAuthRefresh: true,
    );
  }

  Future<Map<String, dynamic>> signOut(SignOutRequest request) {
    return _client.post<Map<String, dynamic>>(
      '/app/auth/sign-out',
      body: request.toJson(),
      decodeData: (data) => (data as Map?)?.cast<String, dynamic>() ?? const {},
    );
  }

  // SIMF-MOB-API-001 §5.1
  Future<CurrentUserDto> getCurrentUser() {
    return _client.get<CurrentUserDto>(
      '/app/users/me',
      decodeData: (data) {
        if (data is! Map<String, dynamic>) {
          throw const FormatException(
            '/users/me response data was not an object.',
          );
        }
        return CurrentUserDto.fromJson(data);
      },
    );
  }

  // Device-key (biometric) sign-in — backend D-172.
  Future<DeviceKeyEntryDto> registerDeviceKey(RegisterDeviceKeyRequest request) {
    return _client.post<DeviceKeyEntryDto>(
      '/app/auth/device-keys',
      body: request.toJson(),
      decodeData: (data) {
        if (data is! Map<String, dynamic>) {
          throw const FormatException(
            'device-key registration response was not an object.',
          );
        }
        return DeviceKeyEntryDto.fromJson(data);
      },
    );
  }

  /// #7a — request an emailed step-up code before enrolling a device key —
  /// backend `POST /app/auth/device-keys/step-up`. Requires a signed-in
  /// approved caller; returns the masked recipient + the code lifetime.
  Future<SendBiometricStepUpResponseDto> sendBiometricStepUp() {
    return _client.post<SendBiometricStepUpResponseDto>(
      '/app/auth/device-keys/step-up',
      decodeData: (data) {
        if (data is! Map<String, dynamic>) {
          throw const FormatException(
            'biometric step-up response was not an object.',
          );
        }
        return SendBiometricStepUpResponseDto.fromJson(data);
      },
    );
  }

  Future<DeviceKeyChallengeDto> issueDeviceKeyChallenge(String deviceKeyId) {
    return _client.post<DeviceKeyChallengeDto>(
      '/app/auth/device-keys/$deviceKeyId/challenge',
      decodeData: (data) {
        if (data is! Map<String, dynamic>) {
          throw const FormatException(
            'device-key challenge response was not an object.',
          );
        }
        return DeviceKeyChallengeDto.fromJson(data);
      },
    );
  }

  Future<TokenPayloadDto> signInWithDeviceKey(
    SignInWithDeviceKeyRequest request,
  ) {
    return _client.post<TokenPayloadDto>(
      '/app/auth/sign-in-with-device-key',
      body: request.toJson(),
      decodeData: _decodeTokenPayload,
    );
  }

  /// Revoke (delete) one of the caller's own device keys — backend
  /// `DELETE /app/auth/device-keys/{id}`. Used when the user turns Face-ID
  /// sign-in off. Requires a signed-in approved caller.
  Future<void> revokeDeviceKey(String deviceKeyId) {
    return _client.delete<void>(
      '/app/auth/device-keys/$deviceKeyId',
      decodeData: (_) {},
    );
  }

  // Password reset — planned, SIMF-API-001 OI-3.
  Future<Map<String, dynamic>> forgotPassword(ForgotPasswordRequest request) {
    return _client.post<Map<String, dynamic>>(
      '/app/auth/forgot-password',
      body: request.toJson(),
      decodeData: (data) => (data as Map?)?.cast<String, dynamic>() ?? const {},
    );
  }

  Future<Map<String, dynamic>> resetPassword(ResetPasswordRequest request) {
    return _client.post<Map<String, dynamic>>(
      '/app/auth/reset-password',
      body: request.toJson(),
      decodeData: (data) => (data as Map?)?.cast<String, dynamic>() ?? const {},
    );
  }

  // Part B (D-430) — badge-QR sign-in / activation.
  Future<Map<String, dynamic>> resolveBadge(ResolveBadgeRequest request) {
    return _client.post<Map<String, dynamic>>(
      '/app/auth/resolve-badge',
      body: request.toJson(),
      decodeData: (data) => (data as Map?)?.cast<String, dynamic>() ?? const {},
    );
  }

  Future<Map<String, dynamic>> badgeActivationStart(
    BadgeActivationStartRequest request,
  ) {
    return _client.post<Map<String, dynamic>>(
      '/app/auth/badge-activation/start',
      body: request.toJson(),
      decodeData: (data) => (data as Map?)?.cast<String, dynamic>() ?? const {},
    );
  }

  Future<Map<String, dynamic>> badgeActivationComplete(
    BadgeActivationCompleteRequest request,
  ) {
    return _client.post<Map<String, dynamic>>(
      '/app/auth/badge-activation/complete',
      body: request.toJson(),
      decodeData: (data) => (data as Map?)?.cast<String, dynamic>() ?? const {},
    );
  }

  static TokenPayloadDto _decodeTokenPayload(Object? data) {
    if (data is! Map<String, dynamic>) {
      throw const FormatException(
        'token response data was not an object.',
      );
    }
    return TokenPayloadDto.fromJson(data);
  }
}
