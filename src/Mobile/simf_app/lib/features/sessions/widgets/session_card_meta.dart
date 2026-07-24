import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';

/// The clock·time·meta line under a session-card title — a 12px beige-hairline
/// glyph + an ellipsized beige label filling the row. Shared by the summaries
/// card (Figma 1388:8439) and the my-sessions card (Figma 1388:9121). Pass
/// EITHER [asset] (an iconify SVG) or [icon] (a Material glyph).
class SessionIconLine extends StatelessWidget {
  const SessionIconLine({required this.text, this.asset, this.icon, super.key})
      : assert(
          (asset == null) != (icon == null),
          'SessionIconLine needs exactly one of asset / icon',
        );

  final String text;
  final String? asset;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final glyph = asset != null
        ? SimfSvgIcon(asset!, size: 12, color: SimfTokens.beigeBorder)
        : Icon(icon, size: 12, color: SimfTokens.beigeBorder);
    return Row(
      children: <Widget>[
        glyph,
        const SizedBox(width: SimfTokens.space2),
        Expanded(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: SimfTokens.labelBeigeSm,
          ),
        ),
      ],
    );
  }
}

/// A speaker / hall meta group — a 24px beige icon box holding a 12px navy
/// glyph, then the ellipsized beige label. Shared by the summaries card (Figma
/// 1388:8445 / 8453) and the my-sessions card (Figma 1388:9127 / 9135). Pass
/// EITHER [asset] (an iconify SVG) or [icon] (a Material glyph).
class SessionMetaGroup extends StatelessWidget {
  const SessionMetaGroup({required this.text, this.asset, this.icon, super.key})
      : assert(
          (asset == null) != (icon == null),
          'SessionMetaGroup needs exactly one of asset / icon',
        );

  final String text;
  final String? asset;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final glyph = asset != null
        ? SimfSvgIcon(asset!, size: 12, color: SimfTokens.navy)
        : Icon(icon, size: 12, color: SimfTokens.navy);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          width: SimfTokens.metaIconBox,
          height: SimfTokens.metaIconBox,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: SimfTokens.beigeBorder,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
          child: glyph,
        ),
        const SizedBox(width: SimfTokens.space2),
        Flexible(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: SimfTokens.labelBeigeSm,
          ),
        ),
      ],
    );
  }
}
