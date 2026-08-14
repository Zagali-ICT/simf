import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_cards.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/app/widgets/tile_body.dart';

/// The home/section tile family: the equal-width row and the nav / stat
/// tiles that fill it. Split out of `simf_page_shell.dart`, which re-exports
/// them so every existing import keeps working.

/// A row of equally sized tiles with the standard gap between them — the
/// frames' 2- and 3-up tile rows (home sections, profile grid).
class SimfTileRow extends StatelessWidget {
  const SimfTileRow({required this.children, super.key});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        for (final (index, child) in children.indexed) ...<Widget>[
          if (index > 0) const SizedBox(width: SimfTokens.space2),
          Expanded(child: child),
        ],
      ],
    );
  }
}

/// One navy feature tile (frame tiles "المتحدثون" / "الجلسات" / …): a 72-high
/// card with a gold icon over a small white label. [enabled] false renders
/// the locked variant (the "بطاقتي" card / the disabled theme tile) on the
/// disabled palette with no tap.
class SimfNavTile extends StatelessWidget {
  const SimfNavTile({
    required this.label,
    this.icon,
    this.iconAsset,
    this.onTap,
    this.enabled = true,
    this.disabledHint,
    this.minHeight = 72,
    super.key,
  }) : assert(
          icon != null || iconAsset != null,
          'SimfNavTile needs either a Material icon or an SVG iconAsset.',
        );

  final String label;

  /// A Material glyph (the default tile icon source).
  final IconData? icon;

  /// An optional bundled SVG asset path (e.g. the KSA frame's exact iconify
  /// glyph). When set it is rendered tinted to the tile colour and takes
  /// precedence over [icon].
  final String? iconAsset;

  final VoidCallback? onTap;
  final bool enabled;

  /// Why the tile is locked, announced as the semantics hint when [enabled] is
  /// false (e.g. "sign in to unlock"). The locked variant is only a colour
  /// change, so without it a screen-reader user met a tile that simply did
  /// nothing and was never told why (BUG-014). Null keeps the plain tile.
  final String? disabledHint;

  /// The tile's minimum height. The frame uses 72 for the "عن الملتقى" row and
  /// 80 for the news + smart-feature rows (758:1216 vs 758:1164).
  final double minHeight;

  @override
  Widget build(BuildContext context) {
    final foreground =
        enabled ? SimfTokens.accent : SimfTokens.navyDisabledText;
    final labelColor =
        enabled ? SimfTokens.surface : SimfTokens.navyDisabledText;
    final asset = iconAsset;
    final Widget top = asset != null
        ? SimfSvgIcon(asset, size: 24, color: foreground)
        : Icon(icon, size: 24, color: foreground);
    final Widget tile = SimfCard(
      onTap: enabled ? onTap : null,
      color: enabled ? SimfTokens.navyDeep : SimfTokens.navyDisabled,
      borderColor:
          enabled ? SimfTokens.beigeBorder : SimfTokens.navyDisabledBorder,
      borderWidth: enabled ? SimfTokens.hairline : 1,
      child: TileBody(
        top: top,
        label: label,
        labelColor: labelColor,
        minHeight: minHeight,
      ),
    );
    final hint = disabledHint;
    if (enabled || hint == null) {
      return tile;
    }
    // The tile stays intentionally inert (it is a locked affordance, not a
    // broken button) — only the announcement changes.
    return Semantics(enabled: false, hint: hint, child: tile);
  }
}

/// A stat tile (frames 512:1780 / 213:963): a big gold number over its label,
/// on the same card chrome as [SimfNavTile].
class SimfStatTile extends StatelessWidget {
  const SimfStatTile({
    required this.value,
    required this.label,
    this.onTap,
    super.key,
  });

  final int value;
  final String label;

  /// Optional tap target. Null (the default) keeps the tile inert — a plain
  /// statistic; non-null makes the whole card tappable via [SimfCard]'s InkWell.
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      onTap: onTap,
      child: TileBody(
        top: Text(
          '$value',
          style: SimfTokens.labelGoldBoldXl,
        ),
        label: label,
        labelColor: SimfTokens.surface,
      ),
    );
  }
}
