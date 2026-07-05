import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/archive_models.dart';

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
          child: _StatTile(
            value: edition.sessions,
            label: l10n.archiveStatSessions,
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _StatTile(
            value: edition.speakers,
            label: l10n.archiveStatSpeakers,
          ),
        ),
      ],
    );
  }
}

class _StatTile extends StatelessWidget {
  const _StatTile({required this.value, required this.label});

  final int value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Column(
        children: <Widget>[
          Text(
            '$value',
            textDirection: TextDirection.ltr,
            style: const TextStyle(
              color: SimfTokens.accent,
              fontSize: SimfTokens.textTitle,
              fontWeight: FontWeight.w600,
              height: 1,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            label,
            textAlign: TextAlign.center,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ],
      ),
    );
  }
}
