import 'package:flutter/material.dart';
import 'package:intl/intl.dart' hide TextDirection;

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../data/notification_models.dart';
import 'notification_category_icon.dart';

/// One 12-hour formatter for the card timestamps (hoisted off the build path).
final DateFormat _timeFormat = DateFormat('hh:mm a');

/// One notification card (frame node): a solid severity-coloured circular icon
/// at the inline end, the bold title + body + "{day} · {time}" line, and the
/// gold unread dot at the inline start.
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
        : _timeFormat.format(item.createdAt!.toLocal());
    // Frame 758:2491 — "{time} · {day}" order.
    final stamp = time == null
        ? null
        : (dayLabel.isEmpty ? time : '$time · $dayLabel');
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
        color: SimfTokens.navyDeep,
        borderColor: Colors.transparent,
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
                          item.localizedTitle(isArabic),
                          style: const TextStyle(
                            color: Colors.white,
                            fontWeight: FontWeight.w600,
                            fontSize: SimfTokens.textLg,
                          ),
                        ),
                        const SizedBox(height: 6),
                        Text(
                          item.localizedBody(isArabic),
                          style: const TextStyle(
                            color: SimfTokens.beigeBorder,
                            fontSize: SimfTokens.textSm,
                            height: 1.5,
                          ),
                        ),
                        if (stamp != null) ...<Widget>[
                          const SizedBox(height: 6),
                          Text(
                            stamp,
                            style: const TextStyle(
                              color: SimfTokens.timestampMuted,
                              fontSize: SimfTokens.textSm,
                            ),
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
                child: _UnreadDot(),
              ),
          ],
        ),
      ),
    );
  }
}

/// The unread marker (frame 758:2491) — a 14-px red dot at the card's top
/// inline-end corner.
class _UnreadDot extends StatelessWidget {
  const _UnreadDot();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 14,
      height: 14,
      decoration: const BoxDecoration(
        color: SimfTokens.danger,
        shape: BoxShape.circle,
      ),
    );
  }
}
