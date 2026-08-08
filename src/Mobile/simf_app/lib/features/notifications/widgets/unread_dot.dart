import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The unread marker (frame 758:2491) — a 14-px red dot at the card's top
/// inline-end corner.
class UnreadDot extends StatelessWidget {
  const UnreadDot();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.unreadDotWidth,
      height: SimfTokens.unreadDotHeight,
      decoration: const BoxDecoration(
        color: SimfTokens.danger,
        shape: BoxShape.circle,
      ),
    );
  }
}
