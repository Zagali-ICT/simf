import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/widgets/legend_item.dart';

/// D-771 — the seat-TIER legend, shown only for a hall that has tiered rows:
/// شخصيات بالغة الأهمية (deep red) · كبار الشخصيات (deep teal) · عادي
/// (bordered). Reuses [LegendItem] so both legends stay one component, and is
/// forced LTR like the state legend so it never mirrors with the RTL page.
class TierLegend extends StatelessWidget {
  const TierLegend({required this.l10n, required this.swatchSize, super.key});

  final AppL10n l10n;
  final double swatchSize;

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Wrap(
        alignment: WrapAlignment.center,
        spacing: SimfTokens.space2,
        runSpacing: SimfTokens.space2,
        children: <Widget>[
          LegendItem(
            label: l10n.seatTierVvip,
            color: SimfTokens.seatTierVvip,
            size: swatchSize,
          ),
          LegendItem(
            label: l10n.seatTierVip,
            color: SimfTokens.seatTierVip,
            size: swatchSize,
          ),
          LegendItem(
            label: l10n.seatTierNormal,
            color: SimfTokens.transparent,
            borderColor: SimfTokens.beigeBorder,
            size: SimfTokens.seatSwatchLg,
          ),
        ],
      ),
    );
  }
}
