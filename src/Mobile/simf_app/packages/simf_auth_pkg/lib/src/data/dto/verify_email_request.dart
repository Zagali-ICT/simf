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
class VerifyOtpRequest {
  const VerifyOtpRequest({required this.otpToken, required this.code});
  final String otpToken;
  final String code;
  Map<String, dynamic> toJson() => <String, dynamic>{
        'otpToken': otpToken,
        'code': code,
      };
}

/// #12 — re-issue the emailed sign-in OTP for an in-progress 2FA ticket.
@immutable
class ResendOtpRequest {
  const ResendOtpRequest({required this.otpToken});
  final String otpToken;
  Map<String, dynamic> toJson() => <String, dynamic>{'otpToken': otpToken};
}

/// #12 — `POST /app/auth/resend-otp` result: the resend-button cooldown (the
/// ticket stays valid, so the client keeps its existing otpToken).
@immutable
class ResendOtpResponseDto {
  const ResendOtpResponseDto({required this.cooldownSeconds});
  factory ResendOtpResponseDto.fromJson(Map<String, dynamic> json) =>
      ResendOtpResponseDto(
        cooldownSeconds: (json['cooldownSeconds'] as num?)?.toInt() ?? 60,
      );
  final int cooldownSeconds;
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
