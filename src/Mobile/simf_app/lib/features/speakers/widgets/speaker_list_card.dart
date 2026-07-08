import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../../app/widgets/simf_svg_icon.dart';
import '../../../core/country_flag.dart';
import '../data/speaker_models.dart';

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
    final label = (speaker.rank != null && speaker.rank!.trim().isNotEmpty)
        ? speaker.rank!.trim()
        : '';

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
            _SpeakerAvatar(
              imageUrl: '$baseUrl/app/assets/SpeakerPhoto/${speaker.id}/image',
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
                          style: const TextStyle(
                            color: Colors.white,
                            fontWeight: FontWeight.w600,
                            fontSize: SimfTokens.textLg,
                          ),
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
                      style: const TextStyle(
                        color: SimfTokens.beigeBorder,
                        fontSize: SimfTokens.textSm,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            // Inline-end caret (frame 908:2089) — the iconamoon thin chevron in
            // GOLD on the trailing (left, in RTL) edge. NOT the beige filled
            // triangle (ic_caret_left): the frame draws a stroked chevron.
            const SimfSvgIcon(
              'assets/icons/ic_back.svg',
              size: 20,
              color: SimfTokens.accent,
            ),
          ],
        ),
      ),
    );
  }
}

/// The 44×44 speaker avatar (frame 908:2004): a **rounded-square (4px)** navy
/// tile on a 0.2px beige hairline showing the speaker's uploaded **photo** (the
/// D-357 `SpeakerPhoto` asset) clipped to the same 4px rounding, falling back to
/// the design's gold **anchor** glyph while it loads or when no photo is set (the
/// asset route 204s).
class _SpeakerAvatar extends StatelessWidget {
  const _SpeakerAvatar({required this.imageUrl});

  final String imageUrl;

  @override
  Widget build(BuildContext context) {
    // Same fallback glyph the detail avatar uses (speaker_placeholder.svg, the
    // Figma 908:2110 gold anchor) so the empty-photo state is consistent across
    // the speaker list and the profile — not a second Material anchor variant.
    const fallback = SimfSvgIcon(
      'assets/icons/speaker_placeholder.svg',
      size: 24,
      color: SimfTokens.accent,
    );
    return Container(
      width: 44,
      height: 44,
      alignment: Alignment.center,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        // Frame 908:2004 — rounded-square (4px) navy tile with a 0.2px beige
        // hairline (no gold fill); the photo covers it (clipped to the same 4px
        // rounding), the gold anchor is the fallback glyph.
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Image.network(
        imageUrl,
        width: 44,
        height: 44,
        fit: BoxFit.cover,
        gaplessPlayback: true,
        loadingBuilder: (context, child, progress) =>
            progress == null ? child : fallback,
        errorBuilder: (context, error, stackTrace) => fallback,
      ),
    );
  }
}
