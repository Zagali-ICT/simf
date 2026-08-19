import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_repository.dart';
import 'package:simf_app/features/exhibitor/widgets/exhibitor_centered.dart';
import 'package:simf_app/features/exhibitor/widgets/my_visitors_list.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

class MyVisitorsBody extends ConsumerWidget {
  const MyVisitorsBody({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    Future<void> refresh() => refreshAsync(ref, myVisitorsProvider.future);

    return ref.watch(myVisitorsProvider).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) {
            // The 403 branch especially: an exhibitor whose booth link lands
            // after the first load would otherwise be stuck on it with no way
            // to re-check, which is why BOTH failures stay refreshable.
            final forbidden = error is ApiFailure && error.httpStatus == 403;
            return SimfRefreshableMessage(
              onRefresh: refresh,
              child: forbidden
                  ? ExhibitorCentered(text: l10n.scanVisitorForbidden)
                  : SimfErrorState(
                      message: l10n.scanVisitorError,
                      retryLabel: l10n.retryLabel,
                      onRetry: () => ref.invalidate(myVisitorsProvider),
                    ),
            );
          },
          data: (visitors) => visitors.isEmpty
              ? SimfRefreshableMessage(
                  onRefresh: refresh,
                  child: ExhibitorCentered(text: l10n.myVisitorsEmpty),
                )
              : MyVisitorsList(visitors: visitors),
        );
  }
}
