import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/utils/http_url.dart';
import '../data/archive_models.dart';

/// The past-speakers row (frame node 927:3347): up to four 72-wide tiles spread
/// across the width — each a 72×72 rounded-rect photo over a centred name. With
/// more than four speakers, the first three show + a bordered "+N / آخرون"
/// overflow card.
class ArchivePastSpeakersRow extends StatelessWidget {
  const ArchivePastSpeakersRow({
    required this.speakers,
    required this.isArabic,
    required this.l10n,
    super.key,
  });

  final List<ArchivePastSpeaker> speakers;
  final bool isArabic;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final hasOverflow = speakers.length > 4;
    final shown =
        (hasOverflow ? speakers.take(3) : speakers.take(4)).toList();
    final overflow = speakers.length - shown.length;
    // Wrap (not Row) so the four fixed-72 tiles spread like the frame on a
    // normal width but wrap to a second line instead of overflowing on a very
    // narrow (~320px) device.
    return Wrap(
      alignment: WrapAlignment.spaceBetween,
      runSpacing: SimfTokens.space3,
      children: <Widget>[
        for (final s in shown)
          _PastSpeakerCard(
            name: s.localized(isArabic),
            photoUrl: s.photoRelativePath,
          ),
        if (overflow > 0)
          _PastSpeakerOverflow(count: overflow, label: l10n.archiveOthersLabel),
      ],
    );
  }
}

/// One past-speaker tile (frame node 927:3346): a 72×72 rounded-rect (r8) photo
/// — the real avatar when [photoUrl] is an absolute http(s) url, else the gold
/// initials — over a centred white 12px SemiBold name.
class _PastSpeakerCard extends StatelessWidget {
  const _PastSpeakerCard({required this.name, this.photoUrl});

  final String name;
  final String? photoUrl;

  @override
  Widget build(BuildContext context) {
    final initials = _speakerInitials(name);
    final fallback = Center(
      child: Text(
        initials,
        style: const TextStyle(
          color: SimfTokens.accent,
          fontWeight: FontWeight.w700,
          fontSize: SimfTokens.textLg,
        ),
      ),
    );
    final showPhoto = isHttpUrl(photoUrl);
    return SizedBox(
      width: 72,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: 72,
            height: 72,
            clipBehavior: Clip.antiAlias,
            decoration: BoxDecoration(
              color: SimfTokens.navyDeep,
              borderRadius: BorderRadius.circular(SimfTokens.radius),
            ),
            child: showPhoto
                ? Image.network(
                    photoUrl!,
                    width: 72,
                    height: 72,
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
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
              height: 1.2,
            ),
          ),
        ],
      ),
    );
  }
}

/// The past-speakers overflow tile (frame node 927:3343): a 72×72 beige-bordered
/// rounded-rect (r8) with a big gold "+N" over the white "آخرون" label.
class _PastSpeakerOverflow extends StatelessWidget {
  const _PastSpeakerOverflow({required this.count, required this.label});

  final int count;
  final String label;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 72,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: 72,
            height: 72,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: SimfTokens.navyDeep,
              borderRadius: BorderRadius.circular(SimfTokens.radius),
              border: Border.all(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
            ),
            child: Text(
              '+$count',
              textDirection: TextDirection.ltr,
              style: const TextStyle(
                color: SimfTokens.accent,
                fontSize: SimfTokens.textTitle,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            label,
            maxLines: 1,
            textAlign: TextAlign.center,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
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
