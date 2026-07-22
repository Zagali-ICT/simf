import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// One large moderator action button (Figma 1462:12236): reject / answered /
/// on-stage. Outline until its state is active; the on-stage CTA is drawn solid
/// (primary) with a drop shadow. Extracted from ModeratorQuestionCard (#16).
class ModeratorActionButton extends StatelessWidget {
  const ModeratorActionButton({
    required this.label,
    required this.icon,
    required this.color,
    required this.filled,
    required this.primary,
    required this.onTap,
    super.key,
  });

  final String label;
  final IconData icon;
  final Color color;
  final bool filled;

  /// The on-stage action is drawn solid by default (Figma); reject/answered are
  /// outline until their state is active.
  final bool primary;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final solid = filled || primary;
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
      child: Container(
        height: SimfTokens.moderatorActionButtonHeight,
        alignment: Alignment.center,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
        decoration: BoxDecoration(
          color: solid
              ? color
              : color.withValues(
                  alpha: SimfTokens.moderatorActionRestingFillOpacity,
                ),
          borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
          border: Border.all(color: color, width: SimfTokens.borderThick),
          boxShadow: primary
              ? <BoxShadow>[
                  BoxShadow(
                    color: color.withValues(
                      alpha: SimfTokens.moderatorScrimOpacity,
                    ),
                    blurRadius: SimfTokens.moderatorActionShadowBlur,
                    offset: const Offset(
                      0,
                      SimfTokens.moderatorActionShadowOffsetY,
                    ),
                  ),
                ]
              : null,
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Icon(icon, size: 30, color: solid ? SimfTokens.surface : color),
            const SizedBox(width: SimfTokens.space3),
            Flexible(
              child: Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: solid ? SimfTokens.surface : color,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textHero - 4, // 24
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
