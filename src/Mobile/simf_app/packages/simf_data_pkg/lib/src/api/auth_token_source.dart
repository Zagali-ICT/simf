import 'dart:async';

/// The data package does not depend on the auth package, but the headers
/// interceptor needs the current access token and the auth-refresh
/// interceptor needs to call refresh + rotate tokens.
///
/// This contract lets the auth package wire those two operations in from
/// outside, without the data package importing anything from
/// `simf_auth_pkg`. The auth package implements [AuthTokenSource] and
/// registers it via the [authTokenSourceProvider] override in the main app.
abstract class AuthTokenSource {
  /// The current access token, or null when the user is signed out.
  /// Read synchronously on every request, so the implementation should
  /// keep an in-memory copy of the token from secure storage rather than
  /// re-reading the keystore on each call.
  String? currentAccessToken();

  /// Called by the auth-refresh interceptor when a 401 is received.
  /// Returns true if a new access token is now available via
  /// [currentAccessToken], false if refresh failed and the session was
  /// cleared. Implementations must serialise concurrent refresh attempts.
  Future<bool> refresh();

  /// Called by the auth-refresh interceptor when refresh itself failed.
  /// The auth package clears its session state; the app router redirects
  /// to sign-in.
  Future<void> onSessionExpired();
}

/// The no-op implementation used while the auth package is not yet wired.
/// Returns null for the access token and false for refresh, which causes
/// protected endpoints to fail with 401 — the correct behaviour for an
/// unauthenticated app run.
class NoAuthTokenSource implements AuthTokenSource {
  const NoAuthTokenSource();

  @override
  String? currentAccessToken() => null;

  @override
  Future<bool> refresh() async => false;

  @override
  Future<void> onSessionExpired() async {}
}
