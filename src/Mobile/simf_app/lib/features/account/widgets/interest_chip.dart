import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// One pill in the sign-up interests grid (Figma 505:1222): gold when
/// [selected], `navyDeep` with a muted border otherwise. Tapping toggles it via
/// [onTap]; [onTap] is null while the form is submitting (disables the pill).
class InterestChip extends StatelessWidget {
  const InterestChip({
    required this.label,
    required this.selected,
    required this.onTap,
    super.key,
  });

  final String label;
  final bool selected;

  /// Null disables the pill (the form is submitting).
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(999),
      child: Container(
        alignment: Alignment.center,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4, vertical: SimfTokens.space2),
        decoration: BoxDecoration(
          color: selected ? SimfTokens.accent : SimfTokens.navyDeep,
          borderRadius: BorderRadius.circular(999),
          border: Border.all(
            width: SimfTokens.interestChipWidth,
            color: selected ? SimfTokens.accent : SimfTokens.chipBorderNavy,
          ),
        ),
        child: Text(
          label,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: SimfTokens.labelWhiteBoldMd,
        ),
      ),
    );
  }
}
