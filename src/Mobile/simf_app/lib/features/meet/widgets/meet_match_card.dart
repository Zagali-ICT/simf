import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/meet_models.dart';

/// One match card (frame `1082:15273`): navy fill, radius 8, the gold % block
/// at the inline end, the name / profile-type / reason column and the gold
/// initials avatar at the inline start.
class MeetMatchCard extends StatelessWidget {
  const MeetMatchCard({
    required this.match,
    required this.isArabic,
    required this.l10n,
    super.key,
  });

  final Recommendation match;
  final bool isArabic;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final name = match.localizedName(isArabic);
    final subtitle = match.localizedProfileType(isArabic) ?? match.jobTitle;
    final reason = _reason(match, l10n);
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        children: <Widget>[
          _Avatar(initials: _avatarInitials(name)),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Text(
                  name,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: SimfTokens.textMd,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                if (subtitle != null && subtitle.trim().isNotEmpty) ...<Widget>[
                  const SizedBox(height: SimfTokens.space2),
                  Text(
                    subtitle,
                    style: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontSize: SimfTokens.textSm,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ],
                if (reason.isNotEmpty) ...<Widget>[
                  const SizedBox(height: SimfTokens.space2),
                  Text(
                    reason,
                    style: TextStyle(
                      color: SimfTokens.beigeBorder.withValues(alpha: 0.6),
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          _PercentBlock(
            percent: _percent(match.score),
            label: l10n.meetPeopleMatchLabel,
          ),
        ],
      ),
    );
  }
}

/// The percent shown on a card — the scorer's `score` is a 0–1 ratio; older
/// payloads that already send 0–100 are passed through. Always 0–100.
int _percent(double score) =>
    (score <= 1 ? score * 100 : score).round().clamp(0, 100);

/// The match-reason line. Prefers the backend-generated bilingual `matchReason`
/// (D-451 — sessions + shared interests); falls back to the shared-interest
/// count for older payloads that don't carry it.
String _reason(Recommendation m, AppL10n l10n) {
  final fromApi = m.localizedMatchReason(l10n.isArabic);
  if (fromApi != null && fromApi.isNotEmpty) {
    return fromApi;
  }
  return m.sharedInterestCount > 0
      ? l10n.meetPeopleSharedInterests(m.sharedInterestCount)
      : '';
}

/// First letters of the first two name words, dot-joined (frame `1082:15157`,
/// "س.أ") — a single letter for a one-word name.
String _avatarInitials(String name) {
  final parts =
      name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
  if (parts.isEmpty) {
    return '?';
  }
  if (parts.length == 1) {
    return parts.first.substring(0, 1);
  }
  return '${parts.first.substring(0, 1)}.${parts[1].substring(0, 1)}';
}

/// The gold % over the `تطابق` label (frame `1082:15270`).
class _PercentBlock extends StatelessWidget {
  const _PercentBlock({required this.percent, required this.label});

  final int percent;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Text(
          '$percent%',
          textDirection: TextDirection.ltr,
          style: const TextStyle(
            color: SimfTokens.accent,
            fontSize: SimfTokens.textXl,
            fontWeight: FontWeight.w600,
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        Text(
          label,
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textSm,
          ),
        ),
      ],
    );
  }
}

/// The gold rounded-square initials avatar (frame `1082:15156`).
class _Avatar extends StatelessWidget {
  const _Avatar({required this.initials});

  final String initials;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 48,
      height: 48,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        initials,
        style: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textMd,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}
