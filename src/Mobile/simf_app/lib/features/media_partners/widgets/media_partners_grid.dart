import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/responsive/grid_columns.dart';
import 'package:simf_app/features/media_partners/data/media_partner_models.dart';
import 'package:simf_app/features/media_partners/widgets/partner_card.dart';

/// The media-partners page body: the grid of partner cards, or the empty state
/// when the feed carries none.
class MediaPartnersGrid extends StatelessWidget {
  const MediaPartnersGrid({
    required this.partners,
    required this.baseUrl,
    required this.onRefresh,
    super.key,
  });

  final List<MediaPartner> partners;
  final String baseUrl;
  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    if (partners.isEmpty) {
      return SimfRefreshableMessage(
        onRefresh: onRefresh,
        child: SimfEmptyState(
          icon: Icons.campaign_outlined,
          message: l10n.mediaPartnersEmpty,
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
        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: responsiveGridColumns(context, compact: 2),
          mainAxisSpacing: SimfTokens.space4,
          crossAxisSpacing: SimfTokens.space4,
          childAspectRatio: SimfTokens.partnerCardAspectRatio,
        ),
        itemCount: partners.length,
        itemBuilder: (context, index) {
          final partner = partners[index];
          return PartnerCard(
            name: partner.localizedName(isArabic: isArabic),
            logoUrl: partner.logoAssetUrl(baseUrl),
          );
        },
      ),
    );
  }
}
