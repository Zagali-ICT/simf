import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';

/// The retry policy every test scope and test container runs under: none.
///
/// Riverpod 3 turns automatic retry ON by default —
/// `ProviderContainer.defaultRetry` re-runs a provider whose fetch threw, up
/// to ten times behind an exponential backoff. Under `flutter_test` that turns
/// an error-branch test from a settled `AsyncError` into a live timer:
/// `pumpAndSettle` cannot settle against one, and the test reports a pending-
/// timer assertion instead of the assertion it was written for. The
/// element-contract sweep failed exactly that way on the 3.4.2 bump.
///
/// Returning `null` means "do not retry", which is the 2.x behaviour this
/// suite's expectations were written against.
Duration? simfTestNoRetry(int retryCount, Object error) => null;

/// The one place a widget test builds its Riverpod scope.
///
/// It is `ProviderScope(overrides: …, child: …)` plus [simfTestNoRetry].
/// Routing the call sites through here makes a policy like that a one-line
/// change in this file instead of an edit in every test file that builds a
/// scope.
///
/// A test that builds a bare `ProviderContainer` instead of a scope cannot use
/// this function, but must still pass [simfTestNoRetry] itself.
Widget simfTestScope({
  required Widget child,
  List<Override> overrides = const <Override>[],
}) {
  return ProviderScope(
    overrides: overrides,
    retry: simfTestNoRetry,
    child: child,
  );
}
