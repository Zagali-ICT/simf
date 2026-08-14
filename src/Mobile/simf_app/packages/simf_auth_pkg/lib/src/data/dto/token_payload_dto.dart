import 'package:meta/meta.dart';
import 'package:simf_auth_pkg/src/data/dto/current_user_dto.dart';
import 'package:simf_auth_pkg/src/domain/session.dart';

/// The shared token payload returned by `/auth/sign-in`, `/auth/verify-totp`,
/// and `/auth/refresh` (SIMF-API-001 §12.2 / §12.4).
@immutable
class TokenPayloadDto {
  const TokenPayloadDto({
    required this.accessToken,
    required this.refreshToken,
    required this.tokenType,
    required this.accessTokenExpiresInSeconds,
    required this.user,
  });

  factory TokenPayloadDto.fromJson(Map<String, dynamic> json) {
    final userJson = json['user'];
    if (userJson is! Map<String, dynamic>) {
      throw const FormatException(
        'Token payload is missing the user object.',
      );
    }
    return TokenPayloadDto(
      accessToken: json['accessToken'] as String? ?? '',
      refreshToken: json['refreshToken'] as String? ?? '',
      tokenType: json['tokenType'] as String? ?? 'Bearer',
      accessTokenExpiresInSeconds:
          (json['accessTokenExpiresInSeconds'] as num?)?.toInt() ?? 1800,
      user: CurrentUserDto.fromJson(userJson),
    );
  }

  final String accessToken;
  final String refreshToken;
  final String tokenType;
  final int accessTokenExpiresInSeconds;
  final CurrentUserDto user;

  /// Maps to the domain [Session]. [issuedAt] defaults to `DateTime.now()`
  /// in the repository; tests can pass a frozen clock.
  Session toSession({required DateTime issuedAt}) {
    return Session(
      accessToken: accessToken,
      refreshToken: refreshToken,
      accessTokenExpiresAt: issuedAt.add(
        Duration(seconds: accessTokenExpiresInSeconds),
      ),
      user: user.toDomain(),
    );
  }
}
