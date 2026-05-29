import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'auth_controller.dart';

/// Convenience provider: resolves the [AuthTokenSource] from the auth
/// controller so the host app can override `simf_data_pkg`'s
/// `authTokenSourceProvider` with one line:
///
/// ```dart
/// authTokenSourceProvider.overrideWith((ref) =>
///   ref.read(authControllerProvider.notifier),
/// ),
/// ```
///
/// This file exists as the documented integration point. Phase 2 may add
/// more providers (form controllers, etc.) here.
AuthTokenSource authTokenSourceFromController(WidgetRef ref) {
  return ref.read(authControllerProvider.notifier);
}
