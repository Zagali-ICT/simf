import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';

/// A speaker photo tile (Figma 908:2004): a rounded-square ([size]px, 4px radius)
/// navy tile on a 0.2px beige hairline showing the speaker's uploaded photo (the
/// D-357 `SpeakerPhoto` asset, built as `{baseUrl}/app/assets/SpeakerPhoto/{id}/image`)
/// clipped to the same rounding, falling back to the design's gold anchor glyph
/// while it loads or when no photo is set (the asset route 404s).
///
/// Shared by the speakers list, the meeting-request speaker picker and the
/// bilateral-meetings card (D-745) so the empty-photo state stays consistent
/// across all three — never a per-screen copy.
class SpeakerPhotoTile extends StatelessWidget {
  const SpeakerPhotoTile({
    required this.imageUrl,
    this.size = 44,
    super.key,
  });

  /// The speaker photo URL, or null/empty when there is no photo to fetch (e.g. a
  /// delegation meeting has no speaker) — the gold anchor placeholder is shown.
  final String? imageUrl;
  final double size;

  @override
  Widget build(BuildContext context) {
    // Keep the speakers-list 24px anchor at the default 44px tile; scale with size.
    final anchorSize = size / 44 * 24;
    final fallback = SimfSvgIcon(
      'assets/icons/speaker_placeholder.svg',
      size: anchorSize,
      color: SimfTokens.accent,
    );
    final url = imageUrl;
    return Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: (url == null || url.isEmpty)
          ? fallback
          : Image.network(
              url,
              width: size,
              height: size,
              fit: BoxFit.cover,
              gaplessPlayback: true,
              loadingBuilder: (context, child, progress) =>
                  progress == null ? child : fallback,
              errorBuilder: (context, error, stackTrace) => fallback,
            ),
    );
  }
}
