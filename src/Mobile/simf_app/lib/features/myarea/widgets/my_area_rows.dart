import 'package:flutter/material.dart';
import 'package:intl/intl.dart' show DateFormat;

import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_forward_chevron.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';

/// One 12-hour formatter for the schedule rows (hoisted off the build path).
final DateFormat _timeFormat = DateFormat('hh:mm a');

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
    final time = _timeFormat.format(saudiOf(item.start));
    final hall = item.localizedHall(isArabic);
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
                    item.localizedTitle(isArabic),
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

/// A جدولي اليوم sub-group header (frame nodes 1041:2042 / 1041:2044): the gold
/// "جلسات" / "مقابلات" label, at the inline start, above each group of rows.
class MyAreaScheduleGroupHeader extends StatelessWidget {
  const MyAreaScheduleGroupHeader({required this.label, super.key});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: Text(
        label,
        style: SimfTokens.labelGoldSemiboldSm,
      ),
    );
  }
}

/// One المزيد row (frame node 512:2126): the label at the inline start and a
/// white forward chevron at the inline end, on the tile chrome.
class MyAreaMoreRow extends StatelessWidget {
  const MyAreaMoreRow({required this.label, required this.onTap, super.key});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space2,
          vertical: SimfTokens.space4,
        ),
        child: Row(
          children: <Widget>[
            Expanded(
              child: Text(
                label,
                style: const TextStyle(
                  color: SimfTokens.surface,
                  fontWeight: FontWeight.w500,
                  fontSize: SimfTokens.textMd,
                ),
              ),
            ),
            const SimfForwardChevron(
              AppAssets.icBack,
              size: SimfTokens.myAreaRowsSize,
              color: SimfTokens.surface,
            ),
          ],
        ),
      ),
    );
  }
}

/// One horizontal share-action pill (frame 758:1283): the gold iconify glyph
/// beside its label on the shared card chrome, fixed to the frame's 48-px row.
class MyAreaShareTile extends StatelessWidget {
  const MyAreaShareTile({
    required this.label,
    required this.iconAsset,
    required this.onTap,
    super.key,
  });

  final String label;
  final String iconAsset;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      onTap: onTap,
      child: SizedBox(
        height: SimfTokens.myAreaRowsHeight,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space3),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              SimfSvgIcon(iconAsset, size: SimfTokens.myAreaRowsSize, color: SimfTokens.accent),
              const SizedBox(width: SimfTokens.space2),
              Flexible(
                child: Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: SimfTokens.labelWhiteSemiboldSm,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
