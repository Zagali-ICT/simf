import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The integration point between the auth package and `simf_data_pkg`'s
/// `authTokenSourceProvider`.
///
/// D-372 — the original wiring
/// (`authTokenSourceProvider.overrideWith((ref) =>
/// ref.read(authControllerProvider.notifier))`) formed a circular provider
/// dependency: controller → repository → api client → token source →
/// controller. Riverpod treats even `read` edges as graph edges, so the
/// cycle cannot be expressed at all — at runtime the interceptors ended up
/// with a never-initialised controller and every authenticated request
/// went out WITHOUT its bearer token (caught by the Wave-1 live E2E run;
/// reproduced by `auth_token_wiring_test.dart`).
///
/// The bridge inverts the registration: it is a passive object with **no
/// provider dependencies** that the host app installs via
/// `authTokenSourceProvider.overrideWith((ref) => AuthTokenBridge())`.
/// `AuthController.build()` then registers itself as the [delegate] —
/// that read goes controller → token source, the direction the graph
/// already allows. Until the controller is built the bridge behaves like
/// `NoAuthTokenSource` (anonymous requests), which is correct for the
/// pre-auth boot phase.
class AuthTokenBridge implements AuthTokenSource {
  /// The live auth controller, registered from `AuthController.build()`.
  AuthTokenSource? delegate;

  @override
  String? currentAccessToken() => delegate?.currentAccessToken();

  @override
  Future<bool> refresh() => delegate?.refresh() ?? Future.value(false);

  @override
  Future<void> onSessionExpired() =>
      delegate?.onSessionExpired() ?? Future.value();
}
