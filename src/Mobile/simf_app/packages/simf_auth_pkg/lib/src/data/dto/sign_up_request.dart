import 'package:meta/meta.dart';

/// Wire shape for `POST /api/v1/auth/sign-up` (SIMF-API-001 §12.4).
@immutable
class SignUpRequest {
  const SignUpRequest({
    required this.email,
    required this.password,
    required this.confirmPassword,
  });

  final String email;
  final String password;
  final String confirmPassword;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'email': email,
        'password': password,
        'confirmPassword': confirmPassword,
      };
}
