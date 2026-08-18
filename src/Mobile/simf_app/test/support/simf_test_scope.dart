import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// The one place a widget test builds its Riverpod scope.
///
/// Today it is exactly `ProviderScope(overrides: …, child: …)` and nothing
/// else. It exists for what comes next: Riverpod 3 turns automatic retry ON by
/// default, so a provider whose fetch throws re-enters loading behind a
/// backoff timer instead of settling into an `AsyncError`. Every error-branch
/// test in this repo would stop failing and start HANGING — `pumpAndSettle`
/// cannot settle against a live timer, and the failure would read as a
/// timeout rather than as the assertion it really is.
///
/// Switching retry off for tests is one argument on the scope. Routing the
/// call sites through here makes that a one-line change in this file instead
/// of an edit in every test file that builds a scope.
Widget simfTestScope({
  required Widget child,
  List<Override> overrides = const <Override>[],
}) {
  return ProviderScope(overrides: overrides, child: child);
}
