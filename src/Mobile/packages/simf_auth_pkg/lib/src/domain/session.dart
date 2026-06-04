import 'package:meta/meta.dart';

import 'current_user.dart';

/// The signed-in session: the access token, the refresh token, and the
/// current user. Held in memory by `AuthController` while the user is
/// signed in; persisted to secure storage so a cold start can restore it.
@immutable
class Session {
  const Session({
    required this.accessToken,
    required this.refreshToken,
    required this.accessTokenExpiresAt,
    required this.user,
  });

  final String accessToken;
  final String refreshToken;
  final DateTime accessTokenExpiresAt;
  final CurrentUser user;

  Session copyWith({
    String? accessToken,
    String? refreshToken,
    DateTime? accessTokenExpiresAt,
    CurrentUser? user,
  }) {
    return Session(
      accessToken: accessToken ?? this.accessToken,
      refreshToken: refreshToken ?? this.refreshToken,
      accessTokenExpiresAt:
          accessTokenExpiresAt ?? this.accessTokenExpiresAt,
      user: user ?? this.user,
    );
  }
}
