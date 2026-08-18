import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// The account's registration status, re-read from the server.
///
/// `GET /app/users/me` via [AuthController.refreshCurrentUser] — the same call
/// the explicit "Re-check" button makes, which is the whole point of the
/// registration-status gate screen. A session-expired failure flips auth to
/// signed-out and the router's gate (route 11) redirects to sign-in; every
/// other failure lands on the error branch.
final registrationStatusProvider =
    FutureProvider.autoDispose<RegistrationStatus>((ref) async {
  final user =
      await ref.watch(authControllerProvider.notifier).refreshCurrentUser();
  return user.registrationStatus;
});
