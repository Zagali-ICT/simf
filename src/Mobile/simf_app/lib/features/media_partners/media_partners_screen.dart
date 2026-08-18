import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/media_coverage_tabs.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/media_partners/data/media_partners_repository.dart';
import 'package:simf_app/features/media_partners/widgets/media_partners_grid.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

// The partner model + provider live in `data/`; re-exported so the existing
// `MediaPartner` / `mediaPartnersProvider` imports (the media-partners test)
// keep resolving off this screen.
export 'data/media_partner_models.dart';
export 'data/media_partners_repository.dart';

/// Media partners — route: `RouteNames.mediaPartners` · Figma 947:3764
class MediaPartnersScreen extends ConsumerWidget {
  const MediaPartnersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final partners = ref.watch(mediaPartnersProvider);
    // The data-package base URL already includes `/api/v1`; the card builds
    // `{base}/app/assets/MediaPartnerLogo/{id}/image` from it.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    Future<void> onRefresh() => refreshAsync(ref, mediaPartnersProvider.future);

    return SimfPageShell(
      // Frame header — the container is "التغطية الإعلامية" (Media coverage),
      // not the bare "الشركاء الإعلاميون" tab label.
      title: l10n.mediaCoverageTitle,
      onBack: () => backOrHome(context),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const Padding(
            padding: EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space2,
              SimfTokens.space4,
              SimfTokens.space2,
            ),
            child: MediaCoverageTabs(active: MediaCoverageTab.partners),
          ),
          Expanded(
            child: partners.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (_, __) => SimfRefreshableMessage(
                onRefresh: onRefresh,
                child: SimfErrorState(
                  message: l10n.mediaPartnersError,
                  retryLabel: l10n.retryLabel,
                  onRetry: () => ref.invalidate(mediaPartnersProvider),
                ),
              ),
              data: (items) => MediaPartnersGrid(
                partners: items,
                baseUrl: baseUrl,
                onRefresh: onRefresh,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
