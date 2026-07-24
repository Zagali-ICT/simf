import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The guided liveness camera (Figma 758:4180 / 758:4248 / 758:4316): the live
/// front-camera preview fills the area above (a spinner until it is ready), and
/// a bottom guidance block shows the current step — a directional cue
/// ([promptLeading]: 😊 for the front step, a gold arrow for the turns) next to
/// its label ([promptText]) — over a 3-dash progress bar (gold = done/current,
/// beige = pending). Capture is automatic once the liveness challenge passes;
/// there is NO manual shutter and NO gallery (live image only, D-662 / D-663).
class LiveCaptureView extends StatelessWidget {
  const LiveCaptureView({
    required this.ready,
    required this.preview,
    required this.humanCheckLabel,
    required this.promptText,
    required this.promptLeading,
    required this.stepIndex,
    required this.stepCount,
    super.key,
  });

  final bool ready;
  final Widget? preview;

  /// The "to confirm you're a real person" line shown above the step command so
  /// the visitor understands why they are being asked to smile / turn (D-683).
  final String humanCheckLabel;
  final String promptText;
  final Widget promptLeading;
  final int stepIndex;
  final int stepCount;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Expanded(
          child: ready && preview != null
              ? SizedBox.expand(
                  child: FittedBox(fit: BoxFit.cover, child: _sized(preview!)),
                )
              : const Center(
                  child: CircularProgressIndicator(color: SimfTokens.accent),
                ),
        ),
        if (ready) _guidance(),
        const SizedBox(height: SimfTokens.space8),
      ],
    );
  }

  Widget _guidance() {
    // The guidance block is laid out left-to-right (icon on the left, label on
    // the right, and the progress filling left→right) exactly as Figma draws it,
    // regardless of the app's RTL direction (Figma 758:4180 / 4248 / 4316).
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            // Why we're asking — the human-check subtitle, then the big command.
            Text(
              humanCheckLabel,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: Colors.white70,
                fontSize: 15,
                fontWeight: FontWeight.w500,
              ),
            ),
            const SizedBox(height: SimfTokens.space4),
            Row(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                promptLeading,
                const SizedBox(width: 10),
                Flexible(
                  child: Text(
                    promptText,
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      color: SimfTokens.surface,
                      fontSize: 26,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: SimfTokens.space5),
            Row(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                for (int i = 0; i < stepCount; i++) ...<Widget>[
                  if (i > 0) const SizedBox(width: SimfTokens.space2),
                  _dash(done: i <= stepIndex),
                ],
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _dash({required bool done}) => Container(
        width: 32,
        height: 6,
        decoration: BoxDecoration(
          color: done ? SimfTokens.accent : SimfTokens.beigeFill50,
          borderRadius: BorderRadius.circular(3),
        ),
      );

  // CameraPreview needs a finite size inside the FittedBox/BoxFit.cover.
  Widget _sized(Widget child) =>
      SizedBox(width: 1080, height: 1440, child: child);
}
