import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/session_lifecycle.dart';
import '../data/session_models.dart' show SessionStatus;

/// A visible session-state chip (owner 2026-07-14) — the small pills the agenda,
/// my-sessions and summary cards show so a session's state reads at a glance.
/// Priority-ordered + mutually exclusive per [sessionStateChips] so a card shows
/// at most one:
/// - [live] while the session is happening (red — the headline state),
/// - [summaryReady] once its محضر is published (gold outline),
/// - [recorded] for a past recorded/published session with no published summary
///   (the existing gold مسجّل badge).
enum SessionChipKind { live, summaryReady, recorded }

/// Picks the chip(s) for a card from its [phase] + capability flags. A live
/// session shows only [SessionChipKind.live]; an ENDED session shows its
/// summary (winning over a bare recording) or a recording; an upcoming session
/// shows none (its recording/summary states can't exist yet).
List<SessionChipKind> sessionStateChips({
  required SessionPhase phase,
  required bool hasPublishedSummary,
  required SessionStatus status,
}) {
  switch (phase) {
    case SessionPhase.live:
      return const <SessionChipKind>[SessionChipKind.live];
    case SessionPhase.upcoming:
      return const <SessionChipKind>[];
    case SessionPhase.ended:
      if (hasPublishedSummary) {
        return const <SessionChipKind>[SessionChipKind.summaryReady];
      }
      final recorded = status == SessionStatus.recorded ||
          status == SessionStatus.published;
      return recorded
          ? const <SessionChipKind>[SessionChipKind.recorded]
          : const <SessionChipKind>[];
  }
}

/// Renders the state chips for a card — a compact [Wrap] (empty when [kinds] is
/// empty). Each card computes [kinds] once (via [sessionStateChips]) and passes
/// it in, so the same list drives both the card's spacer guard and this row (no
/// double `now()` read / recompute). The styling lives here so all three
/// surfaces stay identical.
class SessionStateChipRow extends StatelessWidget {
  const SessionStateChipRow({
    required this.kinds,
    required this.l10n,
    super.key,
  });

  final List<SessionChipKind> kinds;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    if (kinds.isEmpty) {
      return const SizedBox.shrink();
    }
    return Wrap(
      spacing: SimfTokens.space2,
      runSpacing: SimfTokens.space2,
      children: <Widget>[
        for (final kind in kinds)
          SessionStateChip(kind: kind, label: _label(kind)),
      ],
    );
  }

  String _label(SessionChipKind kind) => switch (kind) {
        SessionChipKind.live => l10n.sessionLiveBadge,
        SessionChipKind.summaryReady => l10n.sessionSummaryReadyBadge,
        SessionChipKind.recorded => l10n.sessionRecordedBadge,
      };
}

/// One state chip — a rounded pill with the label and a trailing dot, matching
/// the existing مسجّل badge (label then dot). The live chip is red-filled, the
/// recorded chip gold-filled, the summary-ready chip a gold outline on the card.
class SessionStateChip extends StatelessWidget {
  const SessionStateChip({required this.kind, required this.label, super.key});

  final SessionChipKind kind;
  final String label;

  @override
  Widget build(BuildContext context) {
    final outlined = kind == SessionChipKind.summaryReady;
    final fill = switch (kind) {
      SessionChipKind.live => SimfTokens.liveRed,
      SessionChipKind.recorded => SimfTokens.accent,
      SessionChipKind.summaryReady => SimfTokens.transparent,
    };
    final fg = outlined ? SimfTokens.accent : SimfTokens.surface;
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4, // 16
        vertical: SimfTokens.space2, // 8
      ),
      decoration: BoxDecoration(
        color: fill,
        borderRadius: BorderRadius.circular(SimfTokens.radius), // 8
        border: outlined
            ? Border.all(
                color: SimfTokens.accent,
                width: SimfTokens.hairlineBold,
              )
            : null,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Text(
            label,
            style: TextStyle(
              color: fg,
              fontSize: SimfTokens.textSm, // 12
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Container(
            width: SimfTokens.space2,
            height: SimfTokens.space2,
            decoration: BoxDecoration(color: fg, shape: BoxShape.circle),
          ),
        ],
      ),
    );
  }
}
