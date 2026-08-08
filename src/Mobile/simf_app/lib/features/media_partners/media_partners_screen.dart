import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/media_coverage_tabs.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/media_partners/data/media_partners_repository.dart';
import 'package:simf_app/features/media_partners/widgets/partner_card.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

// The partner model + provider live in `data/`; re-exported so the existing
// `MediaPartner` / `mediaPartnersProvider` imports (the media-partners test)
// keep resolving off this screen.
export 'data/media_partner_models.dart';
export 'data/media_partners_repository.dart';

/// Page 031 — الشركاء الإعلاميون · Media partners (#31, `/media-partners`,
/// Guest+), rebuilt to the KSA-Project frame **947:3764 "Media coverage"** on
/// the shared navy shell.
///
/// **Public.** The frame is the two-tab "المركز الاعلامي" (Media center)
/// container — الشركاء الإعلاميون (this screen, active gold) · احدث المستجدات.
/// The inactive pill navigates to the news (#29) route. The body is a two-column
/// grid of partner cards (frame node 958:2388): a gold rounded-square logo holder
/// over the partner name on the navy KSA card. The logo is the partner's uploaded
/// asset, fetched from the public anonymous route
/// `…/app/assets/MediaPartnerLogo/{id}/image` (the D-357 unified media-asset
/// pipeline) with a loading spinner and a graceful fall-back to the partner's
/// initials on a gold tile when there is no logo or the fetch fails.
class MediaPartnersScreen extends ConsumerWidget {
  const MediaPartnersScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final partners = ref.watch(mediaPartnersProvider);
    // The data-package base URL already includes `/api/v1`; the card builds
    // `{base}/app/assets/MediaPartnerLogo/{id}/image` from it.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    // Pull-to-refresh — re-fetch the media partners (invalidate + await next).
    Future<void> onRefresh() => refreshAsync(ref, mediaPartnersProvider.future);

    return SimfPageShell(
      // Frame header — the container is "التغطية الإعلامية" (Media coverage),
      // not the bare "الشركاء الإعلاميون" tab label.
      title: l10n.mediaCoverageTitle,
      onBack: () => backOrHome(context),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space2,
              SimfTokens.space4,
              SimfTokens.space2,
            ),
            child: const MediaCoverageTabs(active: MediaCoverageTab.partners),
          ),
          Expanded(
            child: partners.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (_, __) => SimfPullToRefresh(
                onRefresh: onRefresh,
                child: SimfPullableHost(
                  child: SimfErrorState(
                    message: l10n.mediaPartnersError,
                    retryLabel: l10n.retryLabel,
                    onRetry: () => ref.invalidate(mediaPartnersProvider),
                  ),
                ),
              ),
              data: (items) {
                if (items.isEmpty) {
                  return SimfPullToRefresh(
                    onRefresh: onRefresh,
                    child: SimfPullableHost(
                      child: SimfEmptyState(
                        icon: Icons.campaign_outlined,
                        message: l10n.mediaPartnersEmpty,
                      ),
                    ),
                  );
                }
                final isArabic = l10n.isArabic;
                // Frame 958:2388 — a 2-column grid of 163.5×104 partner cards
                // with a 16px gap (≈1.57 aspect).
                return SimfPullToRefresh(
                  onRefresh: onRefresh,
                  child: GridView.builder(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(
                      SimfTokens.space4,
                      SimfTokens.space2,
                      SimfTokens.space4,
                      SimfTokens.space6,
                    ),
                    gridDelegate:
                        const SliverGridDelegateWithFixedCrossAxisCount(
                      crossAxisCount: 2,
                      mainAxisSpacing: SimfTokens.space4,
                      crossAxisSpacing: SimfTokens.space4,
                      childAspectRatio: SimfTokens.partnerCardAspectRatio,
                    ),
                    itemCount: items.length,
                    itemBuilder: (context, index) {
                      final partner = items[index];
                      return PartnerCard(
                        name: partner.localizedName(isArabic),
                        logoUrl: partner.logoAssetUrl(baseUrl),
                      );
                    },
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
