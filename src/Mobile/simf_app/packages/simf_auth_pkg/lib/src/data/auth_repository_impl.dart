import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../domain/auth_failure.dart';
import '../domain/current_user.dart';
import '../domain/session.dart';
import '../domain_iface/auth_repository.dart';
import 'auth_api.dart';
import 'device_key_client.dart';
import 'dto/badge_auth_dtos.dart';
import 'dto/device_key_dtos.dart';
import 'dto/sign_in_request.dart';
import 'dto/sign_in_response.dart';
import 'dto/sign_up_request.dart';
import 'dto/verify_email_request.dart';

/// The default [AuthRepository] implementation. Wraps [AuthApi] and turns
/// raw [ApiFailure]s into typed [AuthFailure]s.
///
/// A clock function is injectable so unit tests can pin the
/// access-token-expiry timestamp deterministically.
class AuthRepositoryImpl implements AuthRepository {
  AuthRepositoryImpl({
    required AuthApi api,
    DateTime Function()? now,
  })  : _api = api,
        _now = now ?? DateTime.now;

  final AuthApi _api;
  final DateTime Function() _now;

  @override
  Future<SignInResult> signIn({
    required String email,
    required String password,
  }) async {
    final response = await _guard(
      () => _api.signIn(SignInRequest(email: email, password: password)),
    );
    switch (response) {
      case TokenResponseData(:final payload):
        return SignInSession(payload.toSession(issuedAt: _now()));
      case OtpChallengeResponseData(:final otpToken):
        return SignInOtpChallenge(otpToken);
    }
  }

  @override
  Future<Session> verifyOtp({
    required String otpToken,
    required String code,
  }) async {
    final payload = await _guard(
      () => _api.verifyOtp(
        VerifyOtpRequest(otpToken: otpToken, code: code),
      ),
    );
    return payload.toSession(issuedAt: _now());
  }

  @override
  Future<void> signUp({
    required String email,
    required String password,
    required String confirmPassword,
  }) async {
    await _guard(
      () => _api.signUp(
        SignUpRequest(
          email: email,
          password: password,
          confirmPassword: confirmPassword,
        ),
      ),
    );
  }

  @override
  Future<void> verifyEmail({
    required String email,
    required String code,
  }) async {
    await _guard(
      () => _api.verifyEmail(
        VerifyEmailRequest(email: email, code: code),
      ),
    );
  }

  @override
  Future<int> resendCode({required String email}) async {
    final data = await _guard(
      () => _api.resendCode(ResendCodeRequest(email: email)),
    );
    return (data['codeExpiresInSeconds'] as num?)?.toInt() ?? 0;
  }

  @override
  Future<Session> refresh({required String refreshToken}) async {
    final payload = await _guard(
      () => _api.refresh(RefreshRequest(refreshToken: refreshToken)),
    );
    return payload.toSession(issuedAt: _now());
  }

  @override
  Future<void> signOut({required String refreshToken}) async {
    await _guard(
      () => _api.signOut(SignOutRequest(refreshToken: refreshToken)),
    );
  }

  @override
  Future<CurrentUser> getCurrentUser() async {
    final dto = await _guard(_api.getCurrentUser);
    return dto.toDomain();
  }

  @override
  Future<void> forgotPassword({required String email}) async {
    await _guard(
      () => _api.forgotPassword(ForgotPasswordRequest(email: email)),
    );
  }

  @override
  Future<void> resetPassword({
    required String email,
    required String code,
    required String newPassword,
    required String confirmPassword,
  }) async {
    await _guard(
      () => _api.resetPassword(
        ResetPasswordRequest(
          email: email,
          code: code,
          newPassword: newPassword,
          confirmPassword: confirmPassword,
        ),
      ),
    );
  }

  @override
  Future<({bool found, bool hasPassword, String? displayName, bool needsEmail, String? maskedEmail})>
      resolveBadge({required String qrId}) async {
    final data = await _guard(
      () => _api.resolveBadge(ResolveBadgeRequest(qrId: qrId)),
    );
    return (
      found: data['found'] as bool? ?? false,
      hasPassword: data['hasPassword'] as bool? ?? false,
      displayName: data['displayName'] as String?,
      needsEmail: data['needsEmail'] as bool? ?? false,
      maskedEmail: data['maskedEmail'] as String?,
    );
  }

  @override
  Future<({String maskedEmail, int codeExpiresInSeconds})> badgeActivationStart({
    required String qrId,
    String? email,
  }) async {
    final data = await _guard(
      () => _api.badgeActivationStart(
        BadgeActivationStartRequest(qrId: qrId, email: email),
      ),
    );
    return (
      maskedEmail: data['maskedEmail'] as String? ?? '',
      codeExpiresInSeconds: (data['codeExpiresInSeconds'] as num?)?.toInt() ?? 0,
    );
  }

  @override
  Future<void> badgeActivationComplete({
    required String qrId,
    required String code,
    required String newPassword,
    required String confirmPassword,
  }) async {
    await _guard(
      () => _api.badgeActivationComplete(
        BadgeActivationCompleteRequest(
          qrId: qrId,
          code: code,
          newPassword: newPassword,
          confirmPassword: confirmPassword,
        ),
      ),
    );
  }

  @override
  Future<String> registerDeviceKey({
    required String publicKeySpki,
    required String label,
  }) async {
    final entry = await _guard(
      () => _api.registerDeviceKey(
        RegisterDeviceKeyRequest(
          publicKey: publicKeySpki,
          algorithm: DeviceKeyClient.algorithm,
          label: label,
        ),
      ),
    );
    return entry.id;
  }

  @override
  Future<String> issueDeviceKeyChallenge(String deviceKeyId) async {
    final challenge = await _guard(
      () => _api.issueDeviceKeyChallenge(deviceKeyId),
    );
    return challenge.challenge;
  }

  @override
  Future<Session> signInWithDeviceKey({
    required String deviceKeyId,
    required String challenge,
    required String signature,
  }) async {
    final payload = await _guard(
      () => _api.signInWithDeviceKey(
        SignInWithDeviceKeyRequest(
          deviceKeyId: deviceKeyId,
          challenge: challenge,
          signature: signature,
        ),
      ),
    );
    return payload.toSession(issuedAt: _now());
  }

  @override
  Future<void> revokeDeviceKey(String deviceKeyId) {
    return _guard(() => _api.revokeDeviceKey(deviceKeyId));
  }

  /// Runs [call]. If it throws an [ApiFailure], rethrows the corresponding
  /// [AuthFailure]. Other exceptions are not caught — they indicate a bug,
  /// not a backend failure.
  Future<T> _guard<T>(Future<T> Function() call) async {
    try {
      return await call();
    } on ApiFailure catch (failure) {
      throw mapAuthFailure(failure);
    }
  }
}
