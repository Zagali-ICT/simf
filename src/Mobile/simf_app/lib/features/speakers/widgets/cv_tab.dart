import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

class CvTab extends StatelessWidget {
  const CvTab({
    required this.label,
    required this.selected,
    required this.onTap,
    super.key,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Container(
        height: SimfTokens.controlHeight,
        alignment: Alignment.center,
        padding: const EdgeInsets.all(SimfTokens.space2),
        decoration: BoxDecoration(
          // Figma 912:2312 — the inactive pill is border-only (no fill); it
          // reads the navySurface scaffold through, the active pill is gold.
          color: selected ? SimfTokens.accent : SimfTokens.transparent,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          border: selected
              ? null
              : Border.all(
                  color: SimfTokens.beigeBorder,
                  width: SimfTokens.hairline,
                ),
        ),
        child: Text(
          label,
          textAlign: TextAlign.center,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            color: selected ? SimfTokens.surface : SimfTokens.beigeBorder,
            fontWeight: FontWeight.w600,
            fontSize: SimfTokens.textSm,
            height: SimfTokens.cvTabLineHeight,
          ),
        ),
      ),
    );
  }
}
