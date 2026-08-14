import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/widgets/sub_line.dart';

/// The booth detail sheet (the عرض التفاصيل action). Shows the cached summary
/// immediately; the description streams in from the lazy detail call — a null
/// result (404 / transport) simply omits the description (Page_015 L-5/L-8).
class VenueMapBoothSheet extends StatelessWidget {
  const VenueMapBoothSheet({
    required this.l10n,
    required this.node,
    required this.summary,
    required this.detail,
    super.key,
  });

  final AppL10n l10n;
  final VenueMapNode node;
  final BoothSummary? summary;
  final Future<BoothDetail?> detail;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final booth = summary;
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space5,
        0,
        SimfTokens.space5,
        SimfTokens.space6,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  booth?.localizedName(isArabic) ?? node.localizedLabel(isArabic),
                  style: SimfTokens.titleBold,
                ),
              ),
              if (booth != null)
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: SimfTokens.space2,
                    vertical: SimfTokens.space1,
                  ),
                  decoration: BoxDecoration(
                    color: SimfTokens.field,
                    borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                  ),
                  child: Text(
                    booth.code,
                    style: SimfTokens.codeLabelSm,
                  ),
                ),
            ],
          ),
          if (booth != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            SubLine(
              booth.localizedExhibitor(isArabic),
              booth.localizedSector(isArabic),
            ),
            const SizedBox(height: SimfTokens.space3),
            FutureBuilder<BoothDetail?>(
              future: detail,
              builder: (context, snapshot) {
                if (snapshot.connectionState == ConnectionState.waiting) {
                  return Text(
                    l10n.loadingLabel,
                    style: SimfTokens.bodyInkMuted,
                  );
                }
                final description =
                    snapshot.data?.localizedDescription(isArabic);
                if (description == null) {
                  return const SizedBox.shrink();
                }
                return Text(description);
              },
            ),
          ],
        ],
      ),
    );
  }
}
