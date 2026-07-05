import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

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
      height: 34,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
        itemCount: labels.length,
        separatorBuilder: (_, __) => const SizedBox(width: SimfTokens.space2),
        itemBuilder: (_, index) => _QuickReplyChip(
          label: labels[index],
          onTap: () => onTap(labels[index]),
        ),
      ),
    );
  }
}

class _QuickReplyChip extends StatelessWidget {
  const _QuickReplyChip({required this.label, required this.onTap});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Container(
        alignment: Alignment.center,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space3),
        decoration: BoxDecoration(
          border: Border.all(
            color: SimfTokens.beigeBorder,
            width: SimfTokens.hairline,
          ),
          borderRadius: BorderRadius.circular(SimfTokens.radius),
        ),
        child: Text(
          label,
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textSm,
            height: 18 / 12,
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
    );
  }
}
