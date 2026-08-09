import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

class QuickReplyChip extends StatelessWidget {
  const QuickReplyChip({required this.label, required this.onTap, super.key});

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
          style: SimfTokens.labelBeigeSemibold12Tall,
        ),
      ),
    );
  }
}
