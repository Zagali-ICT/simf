import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'dto/current_user_dto.dart';
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

  Future<TokenPayloadDto> verifyTotp(VerifyTotpRequest request) {
    return _client.post<TokenPayloadDto>(
      '/app/auth/verify-totp',
      body: request.toJson(),
      decodeData: _decodeTokenPayload,
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

  static TokenPayloadDto _decodeTokenPayload(Object? data) {
    if (data is! Map<String, dynamic>) {
      throw const FormatException(
        'token response data was not an object.',
      );
    }
    return TokenPayloadDto.fromJson(data);
  }
}
