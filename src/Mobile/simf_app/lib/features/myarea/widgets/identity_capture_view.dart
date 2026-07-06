import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The full-bleed liveness camera (frames 758:4180 / 758:4248 / 758:4316): the
/// front-camera preview fills the screen edge-to-edge (a spinner until it is
/// ready), with the current liveness-step prompt (ابتسم → أدر رأسك …) shown over
/// it so the user can complete the human check. Capture is automatic once the
/// smile step passes — there is NO manual shutter and NO gallery: the identity
/// photo must be a live, human-verified camera image (owner 2026-07-06, D-662).
class LiveCaptureView extends StatelessWidget {
  const LiveCaptureView({
    required this.ready,
    required this.preview,
    required this.promptText,
    super.key,
  });

  final bool ready;
  final Widget? preview;
  final String promptText;

  @override
  Widget build(BuildContext context) {
    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        if (ready && preview != null)
          SizedBox.expand(
            child: FittedBox(fit: BoxFit.cover, child: _sized(preview!)),
          )
        else
          const Center(
            child: CircularProgressIndicator(color: SimfTokens.accent),
          ),
        if (ready)
          SafeArea(
            child: Align(
              alignment: Alignment.topCenter,
              child: Padding(
                padding: const EdgeInsets.all(SimfTokens.space6),
                child: DecoratedBox(
                  decoration: const BoxDecoration(
                    color: SimfTokens.navyFill90,
                    borderRadius: SimfTokens.borderRadiusSmall,
                  ),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 10,
                    ),
                    child: Text(
                      promptText,
                      textAlign: TextAlign.center,
                      style: const TextStyle(color: Colors.white, fontSize: 16),
                    ),
                  ),
                ),
              ),
            ),
          ),
      ],
    );
  }

  // CameraPreview needs a finite size inside the FittedBox/BoxFit.cover.
  Widget _sized(Widget child) =>
      SizedBox(width: 1080, height: 1440, child: child);
}
