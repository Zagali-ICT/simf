import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../data/seat_map_models.dart';

/// The shared async guard for the seat-map screens (My-Seat #18 + Seat-picker
/// #109): both read the same `GET …/seats` via `seatMapProvider`, so this renders
/// the identical chain once — loading → 404 "session removed" (L-6) → error+retry
/// → "no seat layout" — and hands a laid-out [SessionSeatMap] to [builder] for
/// the screen's own body (the read-only view vs the selectable picker).
class SeatMapAsyncView extends StatelessWidget {
  const SeatMapAsyncView({
    required this.value,
    required this.onRefresh,
    required this.builder,
    super.key,
  });

  final AsyncValue<SessionSeatMap> value;

  /// Re-fetches the seat map. Drives both the pull-to-refresh gesture (every
  /// branch, owner rule) and the error state's Retry button — they did the same
  /// work, so there is one callback rather than two.
  final Future<void> Function() onRefresh;

  final Widget Function(SessionSeatMap map) builder;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return value.when(
      loading: () => const SimfLoadingState(),
      error: (error, _) {
        // A 404 means the session was removed (L-6); anything else is a wire
        // error the user can retry.
        if (error is ApiFailure && error.httpStatus == 404) {
          return SimfRefreshableMessage(
            onRefresh: onRefresh,
            child: SimfEmptyState(
              icon: Icons.event_busy_outlined,
              message: l10n.sessionNotFound,
            ),
          );
        }
        return SimfRefreshableMessage(
          onRefresh: onRefresh,
          child: SimfErrorState(
            message: l10n.seatMapError,
            retryLabel: l10n.retryLabel,
            onRetry: () => unawaited(onRefresh()),
          ),
        );
      },
      data: (map) => map.hasLayout
          ? SimfPullToRefresh(onRefresh: onRefresh, child: builder(map))
          : SimfRefreshableMessage(
              onRefresh: onRefresh,
              child: SimfEmptyState(
                icon: Icons.event_seat_outlined,
                message: l10n.seatMapUnavailable,
              ),
            ),
    );
  }
}
