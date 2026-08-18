import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/widgets/seat_map_async_view.dart';
import 'package:simf_app/features/sessions/widgets/seat_map_view.dart';

/// My seat — مقعدي · route: `RouteNames.mySeat` · Figma 898:2873
class MySeatScreen extends ConsumerWidget {
  const MySeatScreen({required this.sessionId, super.key});

  final String sessionId;

  /// Opens the seat picker in change mode; a `true` pop means the move landed,
  /// so the seat map is re-read. Only `ref` is used after the await — the
  /// context is not touched across the async gap.
  Future<void> _changeSeat(BuildContext context, WidgetRef ref) async {
    final moved = await context.pushNamed<bool>(
      RouteNames.seatPicker,
      pathParameters: <String, String>{RouteParams.sessionId: sessionId},
    );
    if (moved ?? false) {
      ref.invalidate(seatMapProvider(sessionId));
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final value = ref.watch(seatMapProvider(sessionId));
    return SimfPageShell(
      title: l10n.mySeatTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.sessions,
      body: SeatMapAsyncView(
        value: value,
        onRefresh: () => refreshAsync(ref, seatMapProvider(sessionId).future),
        builder: (map) => SeatMapView(
          map: map,
          l10n: l10n,
          onNavigate: () => context.pushNamed(RouteNames.venueMap),
          onShare: map.myCell == null
              ? null
              : () => unawaited(
                    ref.read(seatShareProvider).shareText(
                          l10n.seatShareText(
                            map.myCell!.rowLabel,
                            map.myCell!.seatNumber,
                          ),
                        ),
                  ),
          // B1 — offered only against a seat-specific hold; an open-seating
          // join (general admission) has no seat to move.
          onChangeSeat: map.myCell == null ||
                  map.myCell!.kind == SeatReservationKind.openSeating
              ? null
              : () => unawaited(_changeSeat(context, ref)),
        ),
      ),
    );
  }
}
