import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// A gold time chip showing the local HH:mm (frame 934:3628).
class TimeChip extends StatelessWidget {
  const TimeChip({required this.time});

  final DateTime? time;

  @override
  Widget build(BuildContext context) {
    final t = time;
    final label = t == null
        ? '—'
        : '${t.hour.toString().padLeft(2, '0')}:'
            '${t.minute.toString().padLeft(2, '0')}';
    return Container(
      // Frame 934:3628 — a fixed 53-wide gold chip, p-4, radius-4.
      width: SimfTokens.timeChipWidth,
      padding: const EdgeInsets.all(SimfTokens.space1),
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        textDirection: TextDirection.ltr,
        textAlign: TextAlign.center,
        style: SimfTokens.labelWhiteSemibold,
      ),
    );
  }
}
