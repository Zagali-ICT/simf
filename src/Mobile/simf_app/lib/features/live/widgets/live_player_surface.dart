import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/live/widgets/caption_strip.dart';
import 'package:simf_app/features/live/widgets/live_badges.dart';
import 'package:simf_app/features/live/widgets/live_video_player.dart';

/// The black live player surface (frame 934:3614): the player fills a 16:9 box
/// over a black backdrop, with the LIVE badge + language chip in the top row
/// and the gold-bordered organiser caption strip below the feed.
class LivePlayerSurface extends ConsumerStatefulWidget {
  const LivePlayerSurface({
    required this.url,
    this.liveLabel,
    this.caption,
    super.key,
  });

  final String url;

  /// S-3 — the "LIVE" badge label, or null to HIDE the badge when the session
  /// is not inside its scheduled window (a feed URL may exist before start /
  /// after end without the session being live).
  final String? liveLabel;

  /// P5 — D-439: the admin-typed caption text for this session, or null.
  final String? caption;

  @override
  ConsumerState<LivePlayerSurface> createState() => _LivePlayerSurfaceState();
}

class _LivePlayerSurfaceState extends ConsumerState<LivePlayerSurface> {
  // D-726 (#27) — the watch keep-alive moved INTO the wrapped
  // [LiveVideoPlayer] so it also covers the summary recording / summary-video
  // surfaces that use that player directly (bypassing this surface); this
  // surface no longer owns a timer.

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SimfTokens.black,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            // Frame 934:3612 — the badges sit in a flex row at the top of the
            // black band (LIVE at the inline-start / right, the language chip at
            // the inline-end / left), not overlaid on the video.
            Row(
              children: <Widget>[
                if (widget.liveLabel != null)
                  LiveBadge(label: widget.liveLabel!),
                const Spacer(),
                const LanguageChip(),
              ],
            ),
            const SizedBox(height: SimfTokens.space4),
            LiveVideoPlayer(url: widget.url),
            const SizedBox(height: SimfTokens.space4),
            CaptionStrip(caption: widget.caption),
          ],
        ),
      ),
    );
  }
}
