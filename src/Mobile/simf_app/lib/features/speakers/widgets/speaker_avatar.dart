import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';

/// The speaker profile identity avatar (908:2110 `912:2270`): a 125px white
/// circle ringed gold (2.77px). Renders the speaker's uploaded photo (the D-357
/// `SpeakerPhoto` asset) clipped to the circle, falling back to the gold SIMF
/// anchor placeholder while it loads or when no photo is uploaded (the route
/// 404s).
class SpeakerAvatar extends StatelessWidget {
  const SpeakerAvatar({
    required this.imageUrl,
    required this.initials,
    super.key,
  });

  final String imageUrl;
  final String initials;

  @override
  Widget build(BuildContext context) {
    // The Figma placeholder is the gold SIMF anchor icon on white;
    // text initials are used only when the SVG itself fails to load.
    final placeholder = Center(
      child: SimfSvgIcon(
        'assets/icons/speaker_placeholder.svg',
        size: 64,
        color: SimfTokens.accent,
      ),
    );
    // The gold ring is the outer circle; a 2.77px pad reveals it around the
    // clipped white inner circle, so the photo sits inside the ring on white
    // (never painted under the gold stroke).
    return Center(
      child: Container(
        width: 125,
        height: 125,
        padding: const EdgeInsets.all(2.77),
        decoration: const BoxDecoration(
          color: SimfTokens.accent,
          shape: BoxShape.circle,
        ),
        child: Container(
          clipBehavior: Clip.antiAlias,
          decoration: const BoxDecoration(
            color: SimfTokens.surface,
            shape: BoxShape.circle,
          ),
          child: Image.network(
            imageUrl,
            fit: BoxFit.cover,
            gaplessPlayback: true,
            loadingBuilder: (context, child, progress) =>
                progress == null ? child : placeholder,
            errorBuilder: (context, error, stackTrace) => placeholder,
          ),
        ),
      ),
    );
  }
}
