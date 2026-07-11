import 'package:meta/meta.dart';

/// Part B (D-430) — request DTOs for badge-QR sign-in / activation. The
/// responses are plain envelope maps the repository parses into records, so no
/// response DTO classes are needed.

@immutable
class ResolveBadgeRequest {
  const ResolveBadgeRequest({required this.qrId});

  final String qrId;

  Map<String, dynamic> toJson() => <String, dynamic>{'qrId': qrId};
}

/// D-738 — the password step of badge sign-in: a resolved has-password holder
/// finishes with only their password. The server resolves the user from the
/// qrId and runs the full existing password + 2FA pipeline; the response is the
/// standard sign-in envelope (tokens or the OTP challenge).
@immutable
class BadgeSignInRequest {
  const BadgeSignInRequest({required this.qrId, required this.password});

  final String qrId;
  final String password;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'qrId': qrId,
        'password': password,
      };
}

@immutable
class BadgeActivationStartRequest {
  const BadgeActivationStartRequest({required this.qrId, this.email});

  final String qrId;
  final String? email;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'qrId': qrId,
        if (email != null && email!.isNotEmpty) 'email': email,
      };
}

@immutable
class BadgeActivationCompleteRequest {
  const BadgeActivationCompleteRequest({
    required this.qrId,
    required this.code,
    required this.newPassword,
    required this.confirmPassword,
  });

  final String qrId;
  final String code;
  final String newPassword;
  final String confirmPassword;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'qrId': qrId,
        'code': code,
        'newPassword': newPassword,
        'confirmPassword': confirmPassword,
      };
}
