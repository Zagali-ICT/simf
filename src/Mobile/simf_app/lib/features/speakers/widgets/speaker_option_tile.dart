import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/country_flag.dart';
import '../../../core/net/asset_urls.dart';
import '../data/speaker_models.dart';
import 'speaker_photo_tile.dart';

/// One selectable speaker row in the bilateral picker (owner 2026-07-11): the
/// shared [SpeakerPhotoTile] photo + the speaker's name (with the country flag
/// inline) over the rank line, in a beige-bordered tile that turns gold when
/// selected. Reuses the same photo tile + [countryFlagEmoji] helper as the
/// speakers list so the identity looks identical across the app.
class SpeakerOptionTile extends StatelessWidget {
  const SpeakerOptionTile({
    required this.speaker,
    required this.isArabic,
    required this.baseUrl,
    required this.selected,
    required this.onTap,
    super.key,
  });

  final SpeakerSummary speaker;
  final bool isArabic;
  final String baseUrl;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final flag = countryFlagEmoji(speaker.countryId);
    final rank = speaker.localizedRank(isArabic)?.trim() ?? '';
    return Material(
      color: SimfTokens.surface,
      borderRadius: SimfTokens.borderRadiusSmall,
      child: InkWell(
        onTap: onTap,
        borderRadius: SimfTokens.borderRadiusSmall,
        child: Container(
          padding: const EdgeInsets.all(SimfTokens.space2),
          decoration: BoxDecoration(
            borderRadius: SimfTokens.borderRadiusSmall,
            border: Border.all(
              color: selected ? SimfTokens.accent : SimfTokens.beigeBorder,
              width: selected ? SimfTokens.hairlineBold : SimfTokens.hairline,
            ),
          ),
          child: Row(
            children: <Widget>[
              SpeakerPhotoTile(
                imageUrl:
                    AssetUrls.image(
                      baseUrl,
                      AssetKind.speakerPhoto,
                      speaker.id,
                    ),
                size: 40,
              ),
              const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        Flexible(
                          child: Text(
                            speaker.localizedName(isArabic),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: SimfTokens.labelInkSemibold,
                          ),
                        ),
                        if (flag != null) ...<Widget>[
                          const SizedBox(width: SimfTokens.space2),
                          Text(
                            flag,
                            textDirection: TextDirection.ltr,
                            style: const TextStyle(
                              fontSize: SimfTokens.textSm,
                              height: 1,
                            ),
                          ),
                        ],
                      ],
                    ),
                    if (rank.isNotEmpty) ...<Widget>[
                      const SizedBox(height: SimfTokens.space1),
                      Text(
                        rank,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: SimfTokens.bodyGreySm,
                      ),
                    ],
                  ],
                ),
              ),
              if (selected) ...<Widget>[
                const SizedBox(width: SimfTokens.space2),
                const Icon(
                  Icons.check_circle,
                  color: SimfTokens.accent,
                  size: 20,
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
