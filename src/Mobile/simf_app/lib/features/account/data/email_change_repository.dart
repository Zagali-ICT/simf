import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// #24 — app-local data layer for the self-service change-email flow, over the
/// authenticated `/app/auth/change-email/*` endpoints. Throws [ApiFailure] (from
/// `simf_data_pkg`) on a wire error; the screen surfaces it via
/// `ApiFailure.localizedMessage`. The confirm rolls the server security stamp +
/// revokes sessions, so the screen signs out and re-authenticates on success.
class EmailChangeRepository {
  EmailChangeRepository(this._client);

  final SimfApiClient _client;

  /// Step 1 — email a verification code TO [newEmail]. Returns where it went
  /// (masked) + the code lifetime + the resend-button cooldown.
  Future<EmailChangeCodeSent> sendOtp(String newEmail) {
    return _client.post<EmailChangeCodeSent>(
      '/app/auth/change-email/send-otp',
      body: <String, dynamic>{'newEmail': newEmail},
      decodeData: (data) => EmailChangeCodeSent.fromJson(_asMap(data)),
    );
  }

  /// Step 2 — confirm [newEmail] with the emailed [code] and the account's
  /// [currentPassword] (re-authentication that proves the owner, not just a held
  /// session, is making the change). On success the server moves the login email,
  /// marks it confirmed, rolls the security stamp and revokes every session — so
  /// the caller's token is now dead.
  Future<void> confirm({
    required String newEmail,
    required String code,
    required String currentPassword,
  }) {
    return _client.post<void>(
      '/app/auth/change-email/confirm',
      body: <String, dynamic>{
        'newEmail': newEmail,
        'code': code,
        'currentPassword': currentPassword,
      },
      decodeData: (_) {},
    );
  }

  static Map<String, dynamic> _asMap(Object? data) =>
      (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
}

/// #24 — the result of requesting an email-change code. Never carries the code.
class EmailChangeCodeSent {
  const EmailChangeCodeSent({
    required this.maskedNewEmail,
    required this.expiresInSeconds,
    required this.resendCooldownSeconds,
  });

  factory EmailChangeCodeSent.fromJson(Map<String, dynamic> json) {
    return EmailChangeCodeSent(
      maskedNewEmail: json['maskedNewEmail'] as String? ?? '',
      expiresInSeconds: (json['expiresInSeconds'] as num?)?.toInt() ?? 0,
      resendCooldownSeconds:
          (json['resendCooldownSeconds'] as num?)?.toInt() ?? 0,
    );
  }

  final String maskedNewEmail;
  final int expiresInSeconds;
  final int resendCooldownSeconds;
}

final emailChangeRepositoryProvider = Provider<EmailChangeRepository>((ref) {
  return EmailChangeRepository(ref.watch(simfApiClientProvider));
});
