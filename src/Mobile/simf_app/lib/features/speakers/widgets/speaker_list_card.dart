import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/core/country_flag.dart';
import 'package:simf_app/core/net/asset_urls.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/widgets/speaker_photo_tile.dart';

/// One speaker card on the المتحدثون list (frame 908:1999): the navy [SimfCard]
/// chrome carrying — in RTL — a 44×44 photo tile at the inline start (right),
/// the white name (with the country flag inline at the trailing edge) over the
/// beige rank line, and a small gold caret at the inline end (left). D-432: the
/// host/speaker distinction is per-session (on the session↔speaker join), not a
/// global attribute, so the list shows the anchor for everyone; the host star
/// appears on the session detail.
class SpeakerListCard extends StatelessWidget {
  const SpeakerListCard({
    required this.speaker,
    required this.isArabic,
    required this.baseUrl,
    required this.onTap,
    super.key,
  });

  final SpeakerSummary speaker;
  final bool isArabic;
  final String baseUrl;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Figma 908:1744 (node 1318:3391): the country flag is an inline glyph at the
    // trailing (left, in RTL) edge of the name — NOT a badge on the avatar — and
    // the sub-line carries only the rank.
    final flag = countryFlagEmoji(speaker.countryId);
    final label = speaker.localizedRank(isArabic)?.trim() ?? '';
    final flip = !isArabic;

    return SimfCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        // Figma 908:1744 (Arabic/RTL frame): the photo tile sits at the
        // inline-start (right) beside the name, the navigation caret at the
        // inline-end (left). A Row lays children start→end, so the order is
        // avatar → name → caret.
        child: Row(
          children: <Widget>[
            SpeakerPhotoTile(
              imageUrl:
                  AssetUrls.image(baseUrl, AssetKind.speakerPhoto, speaker.id),
            ),
            const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  // Name with the country flag inline at its trailing (left, in
                  // RTL) edge — Figma node 1318:3391 (flag left of the name, an
                  // 8px gap), right-aligned in the column.
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: <Widget>[
                      Flexible(
                        child: Text(
                          speaker.localizedName(isArabic),
                          textAlign: TextAlign.start,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: SimfTokens.labelWhiteSemiboldLg,
                        ),
                      ),
                      if (flag != null) ...<Widget>[
                        const SizedBox(width: SimfTokens.space2),
                        Text(
                          flag,
                          textDirection: TextDirection.ltr,
                          // Frame 1318:3392 — the flag glyph is 12px.
                          style: const TextStyle(
                            fontSize: SimfTokens.textSm,
                            height: 1,
                          ),
                        ),
                      ],
                    ],
                  ),
                  if (label.isNotEmpty) ...<Widget>[
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      label,
                      textAlign: TextAlign.start,
                      style: SimfTokens.labelBeigeSm,
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            // Inline-end caret (frame 908:2089) — the iconamoon thin chevron in
            // GOLD on the trailing (left, in RTL) edge. NOT the beige filled
            // triangle (ic_caret_left): the frame draws a stroked chevron.
            // Flip horizontally in English so the caret points right →
            // (forward in LTR reading direction).
            Transform.flip(
              flipX: flip,
              child: const SimfSvgIcon(
                AppAssets.icBack,
                size: SimfTokens.speakerListCardSize,
                color: SimfTokens.accent,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

