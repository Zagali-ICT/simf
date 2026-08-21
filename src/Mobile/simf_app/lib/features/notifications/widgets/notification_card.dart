import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/notifications/data/notification_models.dart';
import 'package:simf_app/features/notifications/widgets/notification_category_icon.dart';
import 'package:simf_app/features/notifications/widgets/unread_dot.dart';

/// One notification card (frame node): a solid severity-coloured circular icon
/// at the inline START (the Row's first child), the bold title + body +
/// "{day} · {time}" line, and the unread dot at the inline END
/// (`PositionedDirectional(end:)`), painted `SimfTokens.danger` — red.
///
/// All three of those were backwards here until 2026-08-20: the icon was
/// described at the end, the dot at the start, and the dot called gold.
/// Nothing caught it because a doc comment cannot fail in a way a golden can
/// see — the render was always right; only the description of it was wrong.
class NotificationCard extends StatelessWidget {
  const NotificationCard({
    required this.item,
    required this.isArabic,
    required this.dayLabel,
    required this.onTap,
    super.key,
  });

  final NotificationItem item;
  final bool isArabic;
  final String dayLabel;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final unread = !item.isRead;
    final time = item.createdAt == null
        ? null
        : formatSaudiTime12(saudiOf(item.createdAt!));
    // Frame 758:2491 — "{time} · {day}" order.
    final stamp =
        time == null ? null : (dayLabel.isEmpty ? time : '$time · $dayLabel');
    return Padding(
      padding: const EdgeInsets.only(bottom: SimfTokens.space2),
      // Frame 758:2491 — every card is the navyDeep fill, borderless; the
      // category mark sits at the inline start and an unread card carries a
      // red dot at the top inline-end corner.
      child: SimfCard(
        // Actionable notifications must stay tappable after the inbox
        // auto-marks them read (_openInbox), otherwise the SessionRatingRequest
        // deep-link is unreachable. _onTapItem is a no-op for read,
        // non-actionable rows.
        onTap: onTap,
        borderColor: SimfTokens.transparent,
        borderWidth: 0,
        child: Stack(
          children: <Widget>[
            Padding(
              padding: const EdgeInsets.all(SimfTokens.space2),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  NotificationCategoryIcon(
                    kind: item.kind,
                    severity: item.severity,
                  ),
                  const SizedBox(width: SimfTokens.space3),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          item.localizedTitle(isArabic: isArabic),
                          style: SimfTokens.labelWhiteSemiboldLg,
                        ),
                        const SizedBox(height: SimfTokens.gap6),
                        Text(
                          item.localizedBody(isArabic: isArabic),
                          style: SimfTokens.bodyBeigeSm15,
                        ),
                        if (stamp != null) ...<Widget>[
                          const SizedBox(height: SimfTokens.gap6),
                          Text(
                            stamp,
                            style: SimfTokens.labelTimestampSm,
                          ),
                        ],
                      ],
                    ),
                  ),
                ],
              ),
            ),
            if (unread)
              const PositionedDirectional(
                top: SimfTokens.space2,
                end: SimfTokens.space2,
                child: UnreadDot(),
              ),
          ],
        ),
      ),
    );
  }
}
