import 'package:flutter/material.dart';
import 'package:video_player/video_player.dart';

import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';

const String _worldMapAsset = AppAssets.onboardingWorldMap;

/// The onboarding step background (Figma 148:22): the active step's looping muted
/// video under a navy scrim; until the decoder is ready — or when it never
/// becomes ready (tests, an unsupported runtime, a codec the device refuses) —
/// the world-map poster shows instead.
///
/// Owner 2026-07-26 ("there is a video in the background — not exist / not
/// working") — two defects fixed here:
///  * the poster fallback was gated on `index == 0`, so a failed decode on
///    step 2 / 3 left a **blank navy** screen with nothing behind the copy. The
///    poster now backs EVERY step, so the background is never empty.
///  * the scrim was the 90% photo overlay on the video too, which left roughly
///    a tenth of the footage visible — the video read as "not there". A playing
///    video now sits under the 60% scrim ([SimfTokens.navyFill60]); the still
///    poster keeps the design's 90% ([SimfTokens.navyFill90]).
class OnboardingBackground extends StatelessWidget {
  const OnboardingBackground({
    required this.video,
    required this.videoReady,
    super.key,
  });

  final VideoPlayerController? video;
  final bool videoReady;

  @override
  Widget build(BuildContext context) {
    final controller = video;
    final playing = videoReady && controller != null;
    // Rendered inside a `Positioned.fill` in the screen's outer Stack, so
    // `StackFit.expand` gives the media + overlay tight full-screen bounds.
    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        // The poster is always painted: it is the step background until the
        // decoder is ready, and it stays behind a video whose frames have not
        // arrived yet (no navy flash between steps).
        Image.asset(
          _worldMapAsset,
          fit: BoxFit.cover,
          // A missing/corrupt bundle entry must not take the screen down —
          // the navy scaffold shows through instead.
          errorBuilder: (context, error, stackTrace) =>
              const ColoredBox(color: SimfTokens.navy),
        ),
        if (playing)
          FittedBox(
            fit: BoxFit.cover,
            clipBehavior: Clip.hardEdge,
            child: SizedBox(
              width: controller.value.size.width,
              height: controller.value.size.height,
              child: VideoPlayer(controller),
            ),
          ),
        ColoredBox(
          color: playing ? SimfTokens.navyFill60 : SimfTokens.navyFill90,
        ),
      ],
    );
  }
}
