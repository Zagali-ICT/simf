import 'package:flutter/material.dart';

import '../theme/tokens.dart';

/// Pull-to-refresh chrome. Owner rule: every data page pulls to refresh.
/// Split out of `simf_page_shell.dart` (one widget group per file); that file
/// re-exports these, so every existing import keeps working.

/// Pull-to-refresh wrapper — wrap any data page's scrollable body so a
/// pull-down-from-top re-fetches it (owner rule 2026-06-28: every data page,
/// not just home). The [child] must be (or contain) a scrollable that uses
/// [AlwaysScrollableScrollPhysics] so the gesture fires even when content is
/// short. Styled with the gold spinner on a navy puck to match the shell.
class SimfPullToRefresh extends StatelessWidget {
  const SimfPullToRefresh({required this.onRefresh, required this.child, super.key});

  /// Called when the user pulls to refresh; should re-fetch the page's data.
  final Future<void> Function() onRefresh;

  /// The scrollable body to refresh.
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: onRefresh,
      color: SimfTokens.accent,
      backgroundColor: SimfTokens.navyDeep,
      child: child,
    );
  }
}

/// Hosts a non-scrolling state (empty / error / a single card) inside a
/// viewport-tall, always-scrollable box so a wrapping [SimfPullToRefresh] can still
/// fire its pull-to-refresh gesture on short content. Pair as
/// `SimfPullToRefresh(onRefresh: …, child: SimfPullableHost(child: SimfEmptyState(…)))`.
class SimfPullableHost extends StatelessWidget {
  const SimfPullableHost({required this.child, super.key});

  /// The non-scrolling state widget to host (centred, viewport-tall).
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        return SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          child: ConstrainedBox(
            constraints: BoxConstraints(minHeight: constraints.maxHeight),
            child: child,
          ),
        );
      },
    );
  }
}

/// A pull-to-refresh host for a short message surface (an error or empty state):
/// wraps [child] in [SimfPullToRefresh] + [SimfPullableHost] so a one-line
/// [SimfErrorState] / [SimfEmptyState] stays refreshable and viewport-tall —
/// the pairing screens would otherwise hand-nest at every list branch.
class SimfRefreshableMessage extends StatelessWidget {
  const SimfRefreshableMessage({
    required this.onRefresh,
    required this.child,
    super.key,
  });

  final Future<void> Function() onRefresh;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return SimfPullToRefresh(
      onRefresh: onRefresh,
      child: SimfPullableHost(child: child),
    );
  }
}
