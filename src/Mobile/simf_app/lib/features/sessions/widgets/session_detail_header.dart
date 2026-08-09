import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';

/// The session-detail page header row: the circled back chevron (physical
/// left), the centred title, and — for a moderator — the trailing Q&A control.
/// Mirrors the shell's default header chrome but swaps the notifications/drawer
/// controller for the session-specific moderator action (frame 889:2453).
class SessionDetailHeader extends StatelessWidget {
  const SessionDetailHeader({
    required this.title,
    required this.onBack,
    this.moderateTooltip,
    this.onModerate,
    this.actionIcon = Icons.forum_outlined,
    super.key,
  });

  final String title;
  final VoidCallback onBack;
  final String? moderateTooltip;
  final VoidCallback? onModerate;

  /// D-771 — the trailing action's glyph. Defaults to the moderator Q&A icon; the
  /// staff seating desk passes an event-seat icon through the SAME slot (the two
  /// roles are disjoint, so only one trailing action is ever offered).
  final IconData actionIcon;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space2,
      ),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          SizedBox(
            width: SimfTokens.mapControlSize,
            height: SimfTokens.mapControlSize,
            child: SimfCircledBackButton(onBack: onBack),
          ),
          Expanded(
            child: Text(
              title,
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              // Figma 889:2456 — 18px / SemiBold white.
              style: SimfTokens.labelWhiteSemiboldTitle,
            ),
          ),
          SizedBox(
            width: SimfTokens.mapControlSize,
            height: SimfTokens.mapControlSize,
            child: onModerate == null
                ? null
                : IconButton(
                    tooltip: moderateTooltip,
                    onPressed: onModerate,
                    icon: Icon(
                      actionIcon,
                      color: SimfTokens.surface,
                      size: SimfTokens.sessionDetailHeaderSize,
                    ),
                  ),
          ),
        ],
      ),
    );
  }
}
