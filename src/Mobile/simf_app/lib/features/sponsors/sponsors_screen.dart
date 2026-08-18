import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/sponsors/data/sponsors_repository.dart';
import 'package:simf_app/features/sponsors/widgets/sponsor_tier_list.dart';

/// Sponsors — route: `RouteNames.sponsors` · Figma 922:2824
class SponsorsScreen extends ConsumerWidget {
  const SponsorsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final groups = ref.watch(sponsorGroupsProvider);
    Future<void> onRefresh() => refreshAsync(ref, sponsorGroupsProvider.future);

    return SimfPageShell(
      title: l10n.sponsorsTitle,
      onBack: () => backOrHome(context),
      showSweep: true,
      body: groups.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => SimfRefreshableMessage(
          onRefresh: onRefresh,
          child: SimfErrorState(
            message: l10n.sponsorsError,
            retryLabel: l10n.retryLabel,
            onRetry: () => ref.invalidate(sponsorGroupsProvider),
          ),
        ),
        data: (data) => SponsorTierList(groups: data, onRefresh: onRefresh),
      ),
    );
  }
}
