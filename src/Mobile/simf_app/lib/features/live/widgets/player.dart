import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:video_player/video_player.dart';

/// The `video_player` surface: a 16:9-aware [VideoPlayer] with a play/pause FAB
/// (the HLS/MP4 fallback path).
class Player extends StatelessWidget {
  const Player({required this.controller, required this.onToggle, super.key});

  final VideoPlayerController controller;
  final VoidCallback onToggle;

  @override
  Widget build(BuildContext context) {
    final ratio = controller.value.aspectRatio == 0
        ? SimfTokens.videoAspectRatio
        : controller.value.aspectRatio;
    // The LIVE badge + language chip live in the surface's top row (934:3612).
    return ClipRRect(
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Stack(
        alignment: AlignmentDirectional.bottomEnd,
        children: <Widget>[
          AspectRatio(
            aspectRatio: ratio,
            child: VideoPlayer(controller),
          ),
          Padding(
            padding: const EdgeInsets.all(SimfTokens.space3),
            child: FloatingActionButton.small(
              heroTag: 'live-play',
              onPressed: onToggle,
              child: ValueListenableBuilder<VideoPlayerValue>(
                valueListenable: controller,
                builder: (_, value, __) => Icon(
                  value.isPlaying ? Icons.pause : Icons.play_arrow,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
