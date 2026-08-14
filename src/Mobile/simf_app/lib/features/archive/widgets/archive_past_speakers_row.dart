import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/archive/data/archive_models.dart';
import 'package:simf_app/features/archive/widgets/archive_past_speaker_card.dart';
import 'package:simf_app/features/archive/widgets/past_speaker_overflow.dart';

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
          ArchivePastSpeakerCard(
            name: s.localized(isArabic: isArabic),
            photoUrl: s.photoRelativePath,
          ),
        if (overflow > 0)
          PastSpeakerOverflow(count: overflow, label: l10n.archiveOthersLabel),
      ],
    );
  }
}
