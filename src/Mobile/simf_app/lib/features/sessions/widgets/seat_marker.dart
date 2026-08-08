import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The gold filled marker box on the my-seat card (frame 894:2779): a 44×44
/// gold-bordered tile wrapping a small gold filled square.
class SeatMarker extends StatelessWidget {
  const SeatMarker();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.tapTarget,
      height: SimfTokens.tapTarget,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent.withValues(alpha: SimfTokens.seatFillOpacity),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(color: SimfTokens.accent),
      ),
      child: Container(
        width: SimfTokens.seatMarkerInner,
        height: SimfTokens.seatMarkerInner,
        decoration: BoxDecoration(
          color: SimfTokens.accent,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        ),
      ),
    );
  }
}
