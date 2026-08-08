import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/ai_summary/widgets/summary_content_card.dart';
import 'package:simf_app/features/live/widgets/live_video_player.dart';

/// Item #35 (2026-07-20) — one labeled video on the session-summary surface
/// (screen 34): the gold-bar section heading over the shared [LiveVideoPlayer].
/// Used twice — for the session's FULL live recording and for the team's short
/// summary video — so both players share one presentation. The player is reused
/// from the live feature (the YouTube IFrame for a YouTube URL, `video_player`
/// for an HLS/MP4 fallback — D-349); a fresh `ValueKey(url)` binds one feed per
/// player. The parent only builds this when the URL is non-null, so an absent
/// feed renders nothing at all.
class SummaryVideoCard extends StatelessWidget {
  const SummaryVideoCard({
    required this.label,
    required this.url,
    super.key,
  });

  final String label;
  final String url;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Align(
          alignment: AlignmentDirectional.centerStart,
          child: SummarySectionHeading(label),
        ),
        const SizedBox(height: SimfTokens.space3),
        LiveVideoPlayer(key: ValueKey<String>(url), url: url),
      ],
    );
  }
}
