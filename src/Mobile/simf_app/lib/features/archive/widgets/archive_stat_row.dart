import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/archive_models.dart';
import 'stat_tile.dart';

/// The two stat tiles (frame node 926:3285): a big gold number over its label,
/// in a bordered navy box. The current frame shows exactly **two** tiles —
/// الفعاليات (activities/sessions) and المتحدثون (speakers); it omits the
/// الحضور (attendees) tile the earlier mock carried (owner: match the frame).
class ArchiveStatRow extends StatelessWidget {
  const ArchiveStatRow({required this.l10n, required this.edition, super.key});

  final AppL10n l10n;
  final ArchiveEdition edition;

  @override
  Widget build(BuildContext context) {
    // Frame 926:3285 — RTL, verified against the rendered frame: الفعاليات
    // (activities) at the inline start (right), المتحدثون (speakers) at the
    // inline end (left).
    return Row(
      children: <Widget>[
        Expanded(
          child: StatTile(
            value: edition.sessions,
            label: l10n.archiveStatSessions,
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: StatTile(
            value: edition.speakers,
            label: l10n.archiveStatSpeakers,
          ),
        ),
      ],
    );
  }
}

