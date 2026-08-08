import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The gold index badge (frame 889:2604): a 40×40 gold rounded square with the
/// day-ordinal in white extrabold, always LTR (e.g. "02"); a longer fallback
/// code scales down to fit rather than overflowing the badge.
class IndexBadge extends StatelessWidget {
  const IndexBadge({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.mapControlSize,
      height: SimfTokens.mapControlSize,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radius14),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
        // A two-digit ordinal ("02") shows at full size; a longer fallback code
        // ("S-001" / "S-TODAY") scales down to fit the 40×40 badge instead of
        // overflowing or wrapping.
        child: FittedBox(
          fit: BoxFit.scaleDown,
          child: Text(
            text,
            textDirection: TextDirection.ltr,
            maxLines: 1,
            softWrap: false,
            style: SimfTokens.labelWhiteExtraboldLg,
          ),
        ),
      ),
    );
  }
}
