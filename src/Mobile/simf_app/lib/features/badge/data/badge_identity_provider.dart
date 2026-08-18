import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The badge identity, or **null when the account is not Approved**.
///
/// An `AsyncNotifier` rather than a `FutureProvider`, because the two paths
/// into it differ and a plain provider cannot say so:
///
///   * **build** — a pending account must NOT call the dashboard. It is
///     Approved-only and would 403, and there is a test pinning that it is
///     never called (Page_014 L-1).
///   * **recheck** — a PULL always calls, even while the cached auth state
///     still says pending, because the dashboard's 403 is precisely how
///     approval is discovered. Gating the refresh on the stale local flag is
///     what broke this first time round: the pending user could pull forever
///     and never leave the state.
///
/// The 403 maps to null either way, so "not approved" has one meaning.
///
/// A signed-OUT visitor is NOT handled here: the screen answers that before
/// watching, because a guest has no account rather than an unapproved one.
class BadgeIdentityNotifier extends AsyncNotifier<MyAreaIdentity?> {
  @override
  Future<MyAreaIdentity?> build() async {
    final auth = ref.watch(authControllerProvider);
    final user = auth is AuthStateSignedIn ? auth.session.user : null;
    if (user == null || !user.isApproved) {
      return null;
    }
    return _fetch();
  }

  /// Re-checks approval by actually calling, whatever the cached state says.
  Future<void> recheck() async {
    state = const AsyncValue<MyAreaIdentity?>.loading();
    state = await AsyncValue.guard(_fetch);
  }

  Future<MyAreaIdentity?> _fetch() async {
    try {
      final dashboard = await ref.read(myAreaRepositoryProvider).getDashboard();
      return dashboard.identity;
    } on ApiFailure catch (failure) {
      if (failure.httpStatus == 403) {
        return null;
      }
      rethrow;
    }
  }
}

final badgeIdentityProvider =
    AsyncNotifierProvider.autoDispose<BadgeIdentityNotifier, MyAreaIdentity?>(
  BadgeIdentityNotifier.new,
);
