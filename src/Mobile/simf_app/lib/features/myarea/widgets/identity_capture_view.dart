import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The full-bleed liveness camera (frames 758:4180 / 758:4248 / 758:4316): the
/// front-camera preview fills the screen edge-to-edge (a spinner until it is
/// ready). No framed box / prompt / progress overlay — owner-directed
/// exact-Figma (D-611). The smile → turn → turn challenge still runs in the
/// screen's liveness logic; it is simply not surfaced as on-screen chrome.
class LiveCaptureView extends StatelessWidget {
  const LiveCaptureView({
    required this.ready,
    required this.preview,
    super.key,
  });

  final bool ready;
  final Widget? preview;

  @override
  Widget build(BuildContext context) {
    if (ready && preview != null) {
      return SizedBox.expand(
        child: FittedBox(fit: BoxFit.cover, child: _sized(preview!)),
      );
    }
    return const Center(
      child: CircularProgressIndicator(color: SimfTokens.accent),
    );
  }

  // CameraPreview needs a finite size inside the FittedBox/BoxFit.cover.
  Widget _sized(Widget child) =>
      SizedBox(width: 1080, height: 1440, child: child);
}
