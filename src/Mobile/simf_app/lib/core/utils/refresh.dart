import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart';

/// Re-reads [future] for a pull-to-refresh gesture and swallows a failure.
///
/// Owner rule: every data page pulls to refresh. The screen's own `AsyncValue`
/// already renders the error branch, so the gesture must complete normally —
/// letting the failure through rejects the `RefreshIndicator`'s future and
/// surfaces as an unhandled error on top of the error state the user can see.
///
/// Pass the provider's `.future`; `ref.refresh` invalidates and re-reads it:
/// `onRefresh: () => refreshAsync(ref, sponsorGroupsProvider.future)`.
Future<void> refreshAsync<T>(
  WidgetRef ref,
  Refreshable<Future<T>> future,
) async {
  try {
    // Assigned before awaiting: ref.refresh is @useResult, and awaiting the
    // expression inline does not count as using it.
    final reread = ref.refresh(future);
    await reread;
  } on Object {
    // Surfaced by the screen's error branch, not by the gesture.
  }
}
