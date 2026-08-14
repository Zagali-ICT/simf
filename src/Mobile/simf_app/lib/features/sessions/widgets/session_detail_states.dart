import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';

/// The session detail's four body states: loading, not-found, failed, loaded.
///
/// Split out of `session_detail_screen.dart`, where it was a `_buildBody`
/// method on the State. The screen owns the flags and the reload; this owns
/// which surface each combination renders.
///
/// The not-found and failed surfaces are hosted in an always-scrollable list so
/// a pull-down still fires [SimfPullToRefresh] (pull to retry) even though they
/// render a short, centred surface. That hand-nesting is preserved exactly as
/// the screen had it: the shared [SimfRefreshableMessage] would host the state
/// in a viewport-tall box and so centre it vertically, which is a render change
/// and belongs with the screen's golden re-lock, not with a structural move.
class SessionDetailStates extends StatelessWidget {
  const SessionDetailStates({
    required this.loading,
    required this.notFound,
    required this.failed,
    required this.onRefresh,
    required this.l10n,
    required this.onRetry,
    required this.child,
    super.key,
  });

  /// The first load has not resolved yet.
  final bool loading;

  /// The session id does not resolve (a 404).
  final bool notFound;

  /// The read failed, or resolved without a detail to show.
  final bool failed;

  /// Re-fetch, for both the pull gesture and the error state's retry.
  final Future<void> Function() onRefresh;

  final AppL10n l10n;

  /// The error state's explicit retry control.
  final VoidCallback onRetry;

  /// The loaded detail body, built by the screen.
  final Widget child;

  @override
  Widget build(BuildContext context) {
    if (loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (notFound) {
      return SimfPullToRefresh(
        onRefresh: onRefresh,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: <Widget>[
            SimfEmptyState(
              icon: Icons.event_busy_outlined,
              message: l10n.sessionNotFound,
            ),
          ],
        ),
      );
    }
    if (failed) {
      return SimfPullToRefresh(
        onRefresh: onRefresh,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: <Widget>[
            SimfErrorState(
              message: l10n.sessionDetailError,
              retryLabel: l10n.retryLabel,
              onRetry: onRetry,
            ),
          ],
        ),
      );
    }
    return SimfPullToRefresh(onRefresh: onRefresh, child: child);
  }
}
