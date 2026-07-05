import 'package:flutter/material.dart';
import 'package:video_player/video_player.dart';

import '../../../app/theme/tokens.dart';

const String _worldMapAsset = 'assets/images/onboarding_world_map.jpg';

/// The onboarding step background (Figma 148:22): the active step's looping muted
/// video under a 90%-navy overlay; until the decoder is ready (or when it is
/// unavailable — tests / unsupported runtime) the step-1 world-map photo shows
/// (steps 2–3 fall back to plain navy).
class OnboardingBackground extends StatelessWidget {
  const OnboardingBackground({
    required this.video,
    required this.videoReady,
    required this.index,
    super.key,
  });

  final VideoPlayerController? video;
  final bool videoReady;
  final int index;

  @override
  Widget build(BuildContext context) {
    final controller = video;
    // Rendered inside a `Positioned.fill` in the screen's outer Stack, so
    // `StackFit.expand` gives the media + overlay tight full-screen bounds.
    return Stack(
      fit: StackFit.expand,
      children: <Widget>[
        if (videoReady && controller != null)
          FittedBox(
            fit: BoxFit.cover,
            clipBehavior: Clip.hardEdge,
            child: SizedBox(
              width: controller.value.size.width,
              height: controller.value.size.height,
              child: VideoPlayer(controller),
            ),
          )
        else if (index == 0)
          Image.asset(_worldMapAsset, fit: BoxFit.cover),
        const ColoredBox(color: SimfTokens.navyFill90),
      ],
    );
  }
}
