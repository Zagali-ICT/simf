import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/features/exhibition/widgets/icon_box.dart';

/// A label/value row with a beige-fill icon box on one end and a chevron on the
/// other — the shared shape of the stand-code→map row (Figma 1439:11904) and
/// the website row (1439:11917). The two differ only in fill, line order and
/// weights, passed in by the caller.
class EntityLinkRow extends StatelessWidget {
  const EntityLinkRow({
    required this.label,
    required this.value,
    required this.icon,
    required this.onTap,
    required this.background,
    required this.valueOnTop,
    required this.valueSize,
    required this.valueWeight,
    required this.labelWeight,
    this.iconAsset,
    super.key,
  });

  final String label;
  final String value;
  final IconData icon;

  /// Optional bundled SVG glyph (Figma-exact) rendered in the icon box instead
  /// of [icon] — e.g. the stroked globe on the website row.
  final String? iconAsset;
  final VoidCallback? onTap;

  /// The card fill (navy for the map row, navyDeep for the website row).
  final Color background;

  /// true → value above label (map row); false → label above value (website
  /// row).
  final bool valueOnTop;
  final double valueSize;
  final FontWeight valueWeight;
  final FontWeight labelWeight;

  @override
  Widget build(BuildContext context) {
    // TextAlign.start (not a hardcoded .right) so the row tracks the locale:
    // right in the Arabic design target, left when the language toggle flips to
    // English — matching the shared SimfLinkRow. Codes/URLs are Latin runs so
    // they keep reading order without a forced textDirection.
    final Widget valueText = Text(
      value,
      textAlign: TextAlign.start,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: TextStyle(
        color: SimfTokens.accent,
        fontWeight: valueWeight,
        fontSize: valueSize,
      ),
    );
    final hasLabel = label.isNotEmpty;
    final Widget labelText = Text(
      label,
      textAlign: TextAlign.start,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: TextStyle(
        color: SimfTokens.beigeBorder,
        fontWeight: labelWeight,
        fontSize: SimfTokens.textSm, // 12
      ),
    );
    // Guard the label (an empty one would add a blank line + an 8px gap).
    final lines = valueOnTop
        ? <Widget>[
            valueText,
            if (hasLabel) const SizedBox(height: SimfTokens.space2),
            if (hasLabel) labelText,
          ]
        : <Widget>[
            if (hasLabel) labelText,
            if (hasLabel) const SizedBox(height: SimfTokens.space2),
            valueText,
          ];
    return SimfCard(
      color: background,
      radius: SimfTokens.radius14, // 14
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4), // 16
        child: Row(
          children: <Widget>[
            IconBox(icon: icon, iconAsset: iconAsset),
            const SizedBox(width: SimfTokens.space3), // 12
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                mainAxisSize: MainAxisSize.min,
                children: lines,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            // Figma 1439:11906/11919 — the GOLD thin chevron. Reuses the same
            // bundled ic_back.svg as the sponsor / speaker cards. The asset is
            // authored pointing LEFT for the RTL frame and SimfSvgIcon does not
            // auto-mirror, so mirror it in LTR — a trailing caret then points
            // to the row's end in both directions. (Owner 2026-07-08.)
            Transform.flip(
              flipX: Directionality.of(context) == TextDirection.ltr,
              child: const SimfSvgIcon(
                AppAssets.icBack,
                size: SimfTokens.entityLinkRowSize,
                color: SimfTokens.accent,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
