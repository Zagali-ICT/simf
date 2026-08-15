import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';

/// One schedule row (frame node 512:2116): bold time at the inline start,
/// the title (+ hall, when present), and the gold star at the inline end.
/// Session rows are tappable (→ session detail); meeting rows are not.
class MyAreaScheduleRow extends StatelessWidget {
  const MyAreaScheduleRow({
    required this.item,
    required this.isArabic,
    this.onTap,
    super.key,
  });

  final MyAreaScheduleItem item;
  final bool isArabic;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final time = formatSaudiTime12(saudiOf(item.start));
    final hall = item.localizedHall(isArabic: isArabic);
    return SimfCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space2,
          vertical: SimfTokens.space4,
        ),
        child: Row(
          children: <Widget>[
            Text(
              time,
              textDirection: TextDirection.ltr,
              style: SimfTokens.labelBeigeBoldSm,
            ),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: <Widget>[
                  Text(
                    item.localizedTitle(isArabic: isArabic),
                    style: const TextStyle(
                      color: SimfTokens.surface,
                      fontWeight: FontWeight.w500,
                      fontSize: SimfTokens.textMd,
                    ),
                  ),
                  if (hall != null) ...<Widget>[
                    const SizedBox(height: SimfTokens.space1),
                    Text(
                      hall,
                      style: SimfTokens.bodyBeigeXs,
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space3),
            const Icon(
              Icons.star_rounded,
              size: SimfTokens.myAreaRowsSize,
              color: SimfTokens.accent,
            ),
          ],
        ),
      ),
    );
  }
}
