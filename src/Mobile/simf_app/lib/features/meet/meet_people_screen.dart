import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/meet/data/meet_repository.dart';
import 'package:simf_app/features/meet/widgets/partner_directory_list.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

// `partnerDirectoryProvider` lives in `data/meet_repository.dart`; re-exported so
// the existing imports (the meet screen test) keep resolving off this screen.
export 'data/meet_repository.dart';

/// Meet people — route: RouteNames.meetPeople · no bound Figma node
/// Contract: the curated + opt-in partner directory (`GET
/// /app/networking/partner-directory`), gated by the CP switch — when off the
/// backend returns an empty list. Owner-approved reuse of the existing
/// speakers/sponsors list chrome via the shared `SimfIdentityCell`.
class MeetPeopleScreen extends ConsumerWidget {
  const MeetPeopleScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final directory = ref.watch(partnerDirectoryProvider);
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;

    Future<void> onRefresh() =>
        refreshAsync(ref, partnerDirectoryProvider.future);

    return SimfPageShell(
      title: l10n.meetPeopleTitle,
      onBack: () => backOrHome(context),
      body: directory.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => SimfRefreshableMessage(
          onRefresh: onRefresh,
          child: SimfErrorState(
            message: l10n.meetPeopleError,
            retryLabel: l10n.retryLabel,
            onRetry: () => ref.invalidate(partnerDirectoryProvider),
          ),
        ),
        data: (entries) => PartnerDirectoryList(
          entries: entries,
          isArabic: l10n.isArabic,
          baseUrl: baseUrl,
          emptyMessage: l10n.meetPeopleEmpty,
          onRefresh: onRefresh,
        ),
      ),
    );
  }
}
