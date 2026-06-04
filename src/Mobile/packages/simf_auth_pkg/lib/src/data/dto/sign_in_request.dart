import 'package:meta/meta.dart';

/// Wire shape for `POST /api/v1/auth/sign-in` (SIMF-API-001 §12.4).
@immutable
class SignInRequest {
  const SignInRequest({required this.email, required this.password});

  final String email;
  final String password;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'email': email,
        'password': password,
      };
}
