import 'package:meta/meta.dart';

/// Wire shape for `POST /api/v1/auth/verify-email` (SIMF-API-001 §12.4)
/// and the related forgot/reset/resend-code helpers.
@immutable
class VerifyEmailRequest {
  const VerifyEmailRequest({required this.email, required this.code});

  final String email;
  final String code;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'email': email,
        'code': code,
      };
}

@immutable
class ResendCodeRequest {
  const ResendCodeRequest({required this.email});
  final String email;
  Map<String, dynamic> toJson() => <String, dynamic>{'email': email};
}

@immutable
class RefreshRequest {
  const RefreshRequest({required this.refreshToken});
  final String refreshToken;
  Map<String, dynamic> toJson() => <String, dynamic>{
        'refreshToken': refreshToken,
      };
}

@immutable
class SignOutRequest {
  const SignOutRequest({required this.refreshToken});
  final String refreshToken;
  Map<String, dynamic> toJson() => <String, dynamic>{
        'refreshToken': refreshToken,
      };
}

@immutable
class VerifyTotpRequest {
  const VerifyTotpRequest({required this.mfaToken, required this.code});
  final String mfaToken;
  final String code;
  Map<String, dynamic> toJson() => <String, dynamic>{
        'mfaToken': mfaToken,
        'code': code,
      };
}

@immutable
class ForgotPasswordRequest {
  const ForgotPasswordRequest({required this.email});
  final String email;
  Map<String, dynamic> toJson() => <String, dynamic>{'email': email};
}

@immutable
class ResetPasswordRequest {
  const ResetPasswordRequest({
    required this.email,
    required this.code,
    required this.newPassword,
    required this.confirmPassword,
  });

  final String email;
  final String code;
  final String newPassword;
  final String confirmPassword;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'email': email,
        'code': code,
        'newPassword': newPassword,
        'confirmPassword': confirmPassword,
      };
}
