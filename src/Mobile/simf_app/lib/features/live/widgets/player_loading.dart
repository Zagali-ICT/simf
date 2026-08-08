import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

class PlayerLoading extends StatelessWidget {
  const PlayerLoading();

  @override
  Widget build(BuildContext context) {
    // Frame 934:3595 — the resting/poster affordance: a 52px translucent-white
    // circle holding a 22px white play triangle (shown until the feed renders).
    return AspectRatio(
      aspectRatio: SimfTokens.videoAspectRatio,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: SimfTokens.black,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
        ),
        child: Center(
          child: Container(
            width: SimfTokens.playerLoadingWidth,
            height: SimfTokens.playerLoadingHeight,
            alignment: Alignment.center,
            decoration: const BoxDecoration(
              color: SimfTokens.playScrim,
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.play_arrow,
              size: SimfTokens.playerLoadingSize,
              color: SimfTokens.surface,
            ),
          ),
        ),
      ),
    );
  }
}
