import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

// The decorative diagonal sweep behind a page body.
// Split out of `simf_page_shell.dart` (one widget group per file,
// CLAUDE.md section 1). That file re-exports this one, so every existing
// import of the shell keeps resolving.

/// The decorative rotated sweep block from the KSA entry frames (28.28°,
/// white-4% fill) — the single home for the transform (the auth chrome
/// consumes it too).
class SimfSweepBackground extends StatelessWidget {
  const SimfSweepBackground({super.key});

  @override
  Widget build(BuildContext context) {
    return Positioned(
      top: -156,
      left: 60,
      child: Transform.rotate(
        angle: 0.4936,
        child: Container(
          width: SimfTokens.sweepBlockWidth,
          height: SimfTokens.sweepBlockHeight,
          decoration: const BoxDecoration(
            color: SimfTokens.surfaceTint,
            borderRadius:
                BorderRadius.all(Radius.circular(SimfTokens.radiusSheet)),
          ),
        ),
      ),
    );
  }
}
