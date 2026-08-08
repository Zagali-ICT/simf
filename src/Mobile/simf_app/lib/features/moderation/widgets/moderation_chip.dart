import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

class ModerationChip extends StatelessWidget {
  const ModerationChip({
    required this.label,
    required this.count,
    required this.active,
    required this.onTap,
  });

  final String label;
  final int count;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: Container(
        height: SimfTokens.moderatorFilterChipHeight,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
        decoration: BoxDecoration(
          color: active ? SimfTokens.accent : SimfTokens.navyDeep,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          border: Border.all(
            color: active ? SimfTokens.accent : SimfTokens.navyDisabledBorder,
            width: SimfTokens.moderatorChipBorderWidth,
          ),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Flexible(
              child: Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: active ? SimfTokens.surface : SimfTokens.beigeBorder,
                  fontWeight: active ? FontWeight.w700 : FontWeight.w400,
                  fontSize: SimfTokens.textTitle,
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Container(
              width: SimfTokens.moderatorCountBadgeSize,
              height: SimfTokens.moderatorCountBadgeSize,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: active
                    ? SimfTokens.navy.withValues(
                        alpha: SimfTokens.moderatorCountBadgeActiveOpacity,
                      )
                    : SimfTokens.navyDisabledBorder,
                borderRadius: BorderRadius.circular(SimfTokens.radius5),
              ),
              child: Text(
                '$count',
                style: TextStyle(
                  color: active ? SimfTokens.surface : SimfTokens.accent,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textMd,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
