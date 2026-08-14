import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The About anchor-mark header (frame 1116:16448): the gold anchor + forum
/// name, the optional forum title beneath it, and the optional gold status
/// badge (`{status} · {year}`, shown only when the edition config is loaded —
/// D-495).
class AboutHeader extends StatelessWidget {
  const AboutHeader({
    required this.forumName,
    required this.forumTitle,
    required this.statusBadge,
    super.key,
  });

  final String forumName;

  /// The forum title beneath the name; empty hides the line.
  final String forumTitle;

  /// The gold `{status} · {year}` badge text; null hides the badge.
  final String? statusBadge;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            const Icon(Icons.anchor, color: SimfTokens.accent, size: SimfTokens.aboutHeaderSize),
            const SizedBox(width: SimfTokens.space2),
            Flexible(
              child: Text(
                forumName,
                textAlign: TextAlign.center,
                style: SimfTokens.labelGoldBoldLg,
              ),
            ),
          ],
        ),
        if (forumTitle.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space2),
          Text(
            forumTitle,
            textAlign: TextAlign.center,
            style: SimfTokens.labelWhiteSemibold,
          ),
        ],
        if (statusBadge != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space3),
          Center(
            child: Container(
              padding: const EdgeInsets.symmetric(
                horizontal: SimfTokens.space3,
                vertical: SimfTokens.space1,
              ),
              decoration: BoxDecoration(
                color: SimfTokens.accent,
                borderRadius: BorderRadius.circular(SimfTokens.radius),
              ),
              child: Text(
                statusBadge!,
                style: SimfTokens.labelNavyDeepBoldSm,
              ),
            ),
          ),
        ],
      ],
    );
  }
}
