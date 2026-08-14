import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/widgets/legend_item.dart';

/// The legend row (frame 907:1591): محجوز (deep-navy fill) · متاح (bordered) ·
/// مقعدك (gold fill) — each a label next to its colour swatch. The reserved and
/// mine swatches mirror the in-grid state icons. Reads left-to-right like the
/// frame (forced LTR so it never mirrors with the RTL page).
/// A12 — a fourth entry, "confirmed" (green fill), joins them only when
/// the hall holds a confirmed seat, so the shipped three-item frame is
/// unchanged for a hall nobody has entered yet.
class Legend extends StatelessWidget {
  const Legend({
    required this.l10n,
    required this.availableBorderColor,
    required this.swatchSize,
    required this.showConfirmed,
    super.key,
  });

  final AppL10n l10n;
  final Color availableBorderColor;
  final double swatchSize;
  final bool showConfirmed;

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
            label: l10n.legendReserved,
            color: SimfTokens.navy,
            size: swatchSize,
            icon: Icons.close,
            iconColor: SimfTokens.beigeBorder,
          ),
          if (showConfirmed)
            LegendItem(
              label: l10n.legendConfirmed,
              color: SimfTokens.seatConfirmed,
              size: swatchSize,
              icon: Icons.how_to_reg,
              iconColor: SimfTokens.surface,
            ),
          LegendItem(
            label: l10n.legendAvailable,
            color: SimfTokens.transparent,
            borderColor: availableBorderColor,
            size: SimfTokens.seatSwatchLg,
          ),
          LegendItem(
            label: l10n.legendMine,
            color: SimfTokens.accent,
            size: swatchSize,
            icon: Icons.check,
            iconColor: SimfTokens.navy,
          ),
        ],
      ),
    );
  }
}
