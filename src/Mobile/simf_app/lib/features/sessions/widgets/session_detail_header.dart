import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';

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
    super.key,
  });

  final String title;
  final VoidCallback onBack;
  final String? moderateTooltip;
  final VoidCallback? onModerate;

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
            width: 40,
            height: 40,
            child: SimfCircledBackButton(onBack: onBack),
          ),
          Expanded(
            child: Text(
              title,
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              // Figma 889:2456 — 18px / SemiBold white.
              style: const TextStyle(
                fontSize: SimfTokens.textTitle,
                fontWeight: FontWeight.w600,
                color: Colors.white,
              ),
            ),
          ),
          SizedBox(
            width: 40,
            height: 40,
            child: onModerate == null
                ? null
                : IconButton(
                    tooltip: moderateTooltip,
                    onPressed: onModerate,
                    icon: const Icon(
                      Icons.forum_outlined,
                      color: Colors.white,
                      size: 22,
                    ),
                  ),
          ),
        ],
      ),
    );
  }
}
