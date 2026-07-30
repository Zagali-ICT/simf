import 'package:flutter/material.dart';

import '../theme/tokens.dart';
import 'simf_cards.dart';
import 'simf_svg_icon.dart';

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

  /// The tile's minimum height. The frame uses 72 for the "عن الملتقى" row and
  /// 80 for the news + smart-feature rows (758:1216 vs 758:1164).
  final double minHeight;

  @override
  Widget build(BuildContext context) {
    final foreground =
        enabled ? SimfTokens.accent : SimfTokens.navyDisabledText;
    final labelColor = enabled ? SimfTokens.surface : SimfTokens.navyDisabledText;
    final asset = iconAsset;
    final Widget top = asset != null
        ? SimfSvgIcon(asset, size: 24, color: foreground)
        : Icon(icon, size: 24, color: foreground);
    return SimfCard(
      onTap: enabled ? onTap : null,
      color: enabled ? SimfTokens.navyDeep : SimfTokens.navyDisabled,
      borderColor: enabled
          ? SimfTokens.beigeBorder
          : SimfTokens.navyDisabledBorder,
      borderWidth: enabled ? SimfTokens.hairline : 1,
      child: _TileBody(
        top: top,
        label: label,
        labelColor: labelColor,
        minHeight: minHeight,
      ),
    );
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
      child: _TileBody(
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

/// The shared tile interior: a centred top element over the small bold label.
class _TileBody extends StatelessWidget {
  const _TileBody({
    required this.top,
    required this.label,
    required this.labelColor,
    this.minHeight = 72,
  });

  final Widget top;
  final String label;
  final Color labelColor;
  final double minHeight;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: BoxConstraints(minHeight: minHeight),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            top,
            const SizedBox(height: SimfTokens.space2),
            Text(
              label,
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w600,
                color: labelColor,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
