import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/country_flag.dart';
import 'package:simf_app/core/net/asset_urls.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/speaker_avatar.dart';

/// One speaker card (frame 889:2722/889:2737/889:2747): a navy box with a beige
/// hairline; a 40×40 rounded photo on the inline-start (physical right), with
/// the name (white 16px) + the country flag over the rank (beige 12px) beside
/// it. A session **host** carries the gold star glyph + المضيف on that rank line
/// (PAR-P4a). Tapping opens the speaker profile.
class SessionSpeakerCard extends StatelessWidget {
  const SessionSpeakerCard({
    required this.speaker,
    required this.isArabic,
    required this.hostLabel,
    required this.baseUrl,
    required this.onTap,
    super.key,
  });

  final SessionSpeaker speaker;
  final bool isArabic;
  final String hostLabel;
  final String baseUrl;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final name = speaker.localizedName(isArabic: isArabic);
    final flag = countryFlagEmoji(speaker.countryId);
    final isHost = speaker.role == SessionSpeakerRole.host;
    // The country is now carried by the flag (Figma 889:2726), so the second
    // line is the rank + the host marker only. The rank localizes (Arabic ↔
    // English) to match the name above it (owner 2026-07-19).
    final title = speaker.localizedTitle(isArabic: isArabic)?.trim();
    final rank = title != null && title.isNotEmpty ? title : null;

    return SimfCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Row(
          children: <Widget>[
            SpeakerAvatar(
              imageUrl:
                  AssetUrls.image(baseUrl, AssetKind.speakerPhoto, speaker.id),
            ),
            const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  // Name + flag, hugging the inline-start (physical right under
                  // RTL); the name shrinks before the flag so a long name never
                  // pushes the flag off the card.
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: <Widget>[
                      Flexible(
                        child: Text(
                          name,
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
                          // Frame 1060:12898 — the inline flag glyph is 12px.
                          style: const TextStyle(
                            fontSize: SimfTokens.textSm,
                            height: 1,
                          ),
                        ),
                      ],
                    ],
                  ),
                  if (rank != null || isHost) ...<Widget>[
                    const SizedBox(height: SimfTokens.space2),
                    // PAR-P4a — the host marker is the GOLD STAR glyph beside
                    // the المضيف label, which is what the speakers-list card's
                    // own doc comment (D-432) already promised the detail
                    // renders. A plain speaker keeps the bare rank line.
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        if (rank != null)
                          Flexible(
                            child: Text(
                              rank,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: SimfTokens.labelBeigeSm,
                            ),
                          ),
                        if (rank != null && isHost)
                          const Text(' · ', style: SimfTokens.labelBeigeSm),
                        if (isHost) ...<Widget>[
                          const Icon(
                            Icons.star_rounded,
                            // Matches the header meta glyphs (14) beside the
                            // 12px beige sub-line.
                            size: SimfTokens.sessionSpeakerCardSize,
                            color: SimfTokens.accent,
                          ),
                          const SizedBox(width: SimfTokens.space1),
                          Text(hostLabel, style: SimfTokens.labelBeigeSm),
                        ],
                      ],
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
