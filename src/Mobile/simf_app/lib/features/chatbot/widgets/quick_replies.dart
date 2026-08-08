import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import 'quick_reply_chip.dart';

/// The horizontal quick-reply chip strip (frame `1070:13389`): beige-hairline
/// pills, beige 12px SemiBold text, scrolls past the screen edge. Tapping one
/// sends it as the next prompt.
class QuickReplies extends StatelessWidget {
  const QuickReplies({required this.labels, required this.onTap, super.key});

  final List<String> labels;
  final ValueChanged<String> onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: SimfTokens.quickReplyStripHeight,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
        itemCount: labels.length,
        separatorBuilder: (_, __) => const SizedBox(width: SimfTokens.space2),
        itemBuilder: (_, index) => QuickReplyChip(
          label: labels[index],
          onTap: () => onTap(labels[index]),
        ),
      ),
    );
  }
}

