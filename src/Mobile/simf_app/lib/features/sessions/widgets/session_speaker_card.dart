import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../../core/country_flag.dart';
import '../data/session_models.dart';

/// One speaker card (frame 889:2722/889:2737/889:2747): a navy box with a beige
/// hairline; a 40×40 rounded photo on the inline-start (physical right), with
/// the name (white 16px) + the country flag over the rank (beige 12px) beside
/// it. Tapping opens the speaker profile.
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
    final name = speaker.localizedName(isArabic);
    final flag = countryFlagEmoji(speaker.countryId);
    final isHost = speaker.role == SessionSpeakerRole.host;
    // The country is now carried by the flag (Figma 889:2726), so the second
    // line is the rank + the host marker only.
    final subParts = <String>[
      if (speaker.title != null && speaker.title!.trim().isNotEmpty)
        speaker.title!.trim(),
      if (isHost) hostLabel,
    ];

    return SimfCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Row(
          children: <Widget>[
            _SpeakerAvatar(
              imageUrl: '$baseUrl/app/assets/SpeakerPhoto/${speaker.id}/image',
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
                          // Frame 1060:12898 — the inline flag glyph is 12px.
                          style: const TextStyle(
                            fontSize: SimfTokens.textSm,
                            height: 1,
                          ),
                        ),
                      ],
                    ],
                  ),
                  if (subParts.isNotEmpty) ...<Widget>[
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      subParts.join(' · '),
                      style: const TextStyle(
                        color: SimfTokens.beigeBorder,
                        fontSize: SimfTokens.textSm,
                      ),
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

/// The speaker's photo on a speaker card (frame 1060:12892): a 40×40 rounded
/// square with a beige hairline. Renders the uploaded SpeakerPhoto asset
/// (D-357), falling back to a navy person glyph while it loads or when the
/// speaker has no photo (the asset route 404s).
class _SpeakerAvatar extends StatelessWidget {
  const _SpeakerAvatar({required this.imageUrl});

  final String imageUrl;

  @override
  Widget build(BuildContext context) {
    const placeholder = ColoredBox(
      color: SimfTokens.navy,
      child: Center(
        child: Icon(Icons.person, size: 20, color: SimfTokens.beigeBorder),
      ),
    );
    return Container(
      width: 40,
      height: 40,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Image.network(
        imageUrl,
        fit: BoxFit.cover,
        gaplessPlayback: true,
        loadingBuilder: (context, child, progress) =>
            progress == null ? child : placeholder,
        errorBuilder: (context, error, stackTrace) => placeholder,
      ),
    );
  }
}
