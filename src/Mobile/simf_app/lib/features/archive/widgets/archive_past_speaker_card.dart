import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/utils/http_url.dart';

/// One past-speaker tile (frame node 927:3346): a 72×72 rounded-rect (r8) photo
/// — the real avatar when [photoUrl] is an absolute http(s) url, else the gold
/// initials — over a centred white 12px SemiBold name.
class ArchivePastSpeakerCard extends StatelessWidget {
  const ArchivePastSpeakerCard({required this.name, this.photoUrl, super.key});

  final String name;
  final String? photoUrl;

  @override
  Widget build(BuildContext context) {
    final initials = _speakerInitials(name);
    final fallback = Center(
      child: Text(initials, style: SimfTokens.labelGoldBoldLg),
    );
    final showPhoto = isHttpUrl(photoUrl);
    return SizedBox(
      width: SimfTokens.archivePastSpeakerCardWidth,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: SimfTokens.archivePastSpeakerCardWidth,
            height: SimfTokens.archivePastSpeakerCardHeight,
            clipBehavior: Clip.antiAlias,
            decoration: BoxDecoration(
              color: SimfTokens.navyDeep,
              borderRadius: BorderRadius.circular(SimfTokens.radius),
            ),
            child: showPhoto
                ? Image.network(
                    photoUrl!,
                    width: SimfTokens.archivePastSpeakerCardWidth,
                    height: SimfTokens.archivePastSpeakerCardHeight,
                    fit: BoxFit.cover,
                    gaplessPlayback: true,
                    loadingBuilder: (context, child, progress) =>
                        progress == null ? child : fallback,
                    errorBuilder: (context, error, stackTrace) => fallback,
                  )
                : fallback,
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            name,
            maxLines: 2,
            textAlign: TextAlign.center,
            overflow: TextOverflow.ellipsis,
            style: SimfTokens.labelWhiteSemiboldSmTall,
          ),
        ],
      ),
    );
  }
}

/// The first letters of up to two words of a speaker name, for the avatar
/// fallback.
String _speakerInitials(String name) {
  final trimmed = name.trim();
  if (trimmed.isEmpty) {
    return '—';
  }
  return trimmed
      .split(RegExp(r'\s+'))
      .where((w) => w.isNotEmpty)
      .take(2)
      .map((w) => w.characters.first)
      .join();
}
