import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:youtube_player_iframe/youtube_player_iframe.dart';

/// The YouTube IFrame player surface (D-349) with a LIVE badge overlay. YouTube
/// supplies its own play/pause + CC controls (the latter covers الترجمة الفورية
/// for YouTube feeds), so no extra play FAB is added here.
class YoutubeView extends StatelessWidget {
  const YoutubeView({required this.controller, super.key});

  final YoutubePlayerController controller;

  @override
  Widget build(BuildContext context) {
    // The LIVE badge + language chip live in the surface's top row (934:3612),
    // not overlaid on the video, so the player is just the rounded feed.
    return ClipRRect(
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: YoutubePlayer(
        controller: controller,
      ),
    );
  }
}
