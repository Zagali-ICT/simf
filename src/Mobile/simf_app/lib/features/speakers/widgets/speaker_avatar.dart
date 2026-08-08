import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_logo_image.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';

/// The speaker profile identity avatar (908:2110 `912:2270`): a 125px white
/// circle ringed gold (2.77px). Renders the speaker's uploaded photo (the D-357
/// `SpeakerPhoto` asset) clipped to the circle, falling back to the gold SIMF
/// anchor placeholder while it loads or when no photo is uploaded (the route
/// 404s).
///
/// Owner 2026-07-26 — a PORTRAIT keeps `BoxFit.cover` (the circle must stay
/// filled, never letterboxed) but tapping it now opens the photo full size via
/// the shared [SimfLogoImage].
class SpeakerAvatar extends StatelessWidget {
  const SpeakerAvatar({
    required this.imageUrl,
    required this.initials,
    required this.name,
    super.key,
  });

  final String imageUrl;
  final String initials;

  /// The speaker's localized name — the photo's accessible name and the
  /// full-size viewer's title.
  final String name;

  @override
  Widget build(BuildContext context) {
    // The Figma placeholder is the gold SIMF anchor icon on white;
    // text initials are used only when the SVG itself fails to load.
    const placeholder = Center(
      child: SimfSvgIcon(
        AppAssets.speakerPlaceholder,
        size: 64,
        color: SimfTokens.accent,
      ),
    );
    // The gold ring is the outer circle; a 2.77px pad reveals it around the
    // clipped white inner circle, so the photo sits inside the ring on white
    // (never painted under the gold stroke).
    return Center(
      child: Container(
        width: SimfTokens.speakerAvatarWidth,
        height: SimfTokens.speakerAvatarHeight,
        padding: const EdgeInsets.all(SimfTokens.avatarRingInset),
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
          child: SimfLogoImage(
            url: imageUrl,
            placeholder: placeholder,
            semanticLabel: name,
            fit: BoxFit.cover,
            width: double.infinity,
            height: double.infinity,
          ),
        ),
      ),
    );
  }
}
