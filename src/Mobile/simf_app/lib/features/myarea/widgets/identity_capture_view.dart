import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/liveness.dart';

/// The live-capture face frame (frames 758:4180 / 758:4248 / 758:4316): the
/// gold-bordered 3:4 preview box (a spinner until the camera is ready), the
/// per-step prompt, and the gold progress indicator. Data + the camera preview
/// are supplied by the screen State; this widget is pure presentation.
class LiveCaptureView extends StatelessWidget {
  const LiveCaptureView({
    required this.l10n,
    required this.step,
    required this.activeIndex,
    required this.ready,
    required this.preview,
    super.key,
  });

  final AppL10n l10n;
  final LivenessStep step;

  /// Position in the shuffled sequence (0-based) — drives the progress dots, not
  /// the enum index (the challenge order is shuffled per session, D-422, so the
  /// enum index no longer matches the displayed order).
  final int activeIndex;
  final bool ready;
  final Widget? preview;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        // The framed face preview (the frame's bordered square) — Expanded so it
        // scales to the available height without overflowing.
        Expanded(
          child: Center(
            child: Padding(
              padding:
                  const EdgeInsets.symmetric(horizontal: SimfTokens.space6),
              child: AspectRatio(
                aspectRatio: 3 / 4,
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    color: SimfTokens.navyDeep,
                    borderRadius: BorderRadius.circular(SimfTokens.radius),
                    border: Border.all(color: SimfTokens.accent, width: 2),
                  ),
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(SimfTokens.radius),
                    child: ready && preview != null
                        ? FittedBox(fit: BoxFit.cover, child: _sized(preview!))
                        : const Center(
                            child: CircularProgressIndicator(
                              color: SimfTokens.accent,
                            ),
                          ),
                  ),
                ),
              ),
            ),
          ),
        ),
        const SizedBox(height: SimfTokens.space6),
        Text(
          livenessPrompt(l10n, step),
          textAlign: TextAlign.center,
          style: const TextStyle(
            color: Colors.white,
            fontSize: SimfTokens.textLg,
            fontWeight: FontWeight.w600,
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        // D-610 fix: the progress reflects the position in the shuffled sequence
        // ([activeIndex]) — NOT `step.index` (the enum order), which no longer
        // matches the displayed order once the challenge is shuffled (D-422).
        LivenessProgressDots(activeIndex: activeIndex),
        const SizedBox(height: SimfTokens.space6),
      ],
    );
  }

  // CameraPreview needs a finite size inside the FittedBox/BoxFit.cover.
  Widget _sized(Widget child) =>
      SizedBox(width: 1080, height: 1440, child: child);
}

/// The gold step indicator (the frame's bottom progress): one bar per liveness
/// step, gold up to and including [activeIndex].
class LivenessProgressDots extends StatelessWidget {
  const LivenessProgressDots({required this.activeIndex, super.key});

  final int activeIndex;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        for (var i = 0; i < LivenessStep.values.length; i++)
          Container(
            margin: const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
            width: i <= activeIndex ? 28 : 20,
            height: 6,
            decoration: BoxDecoration(
              color: i <= activeIndex
                  ? SimfTokens.accent
                  : SimfTokens.beigeBorder.withValues(alpha: 0.4),
              borderRadius: BorderRadius.circular(3),
            ),
          ),
      ],
    );
  }
}
