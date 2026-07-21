import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/route_names.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../sessions/data/session_models.dart';
import '../../sessions/widgets/favourite_heart_button.dart';
import '../../sessions/widgets/session_card_meta.dart';
import '../../sessions/widgets/session_state_chip.dart';

/// One rich session-summary card (Figma 1388:8392): heart on the trailing edge,
/// the title over the clock·time·duration line, the primary speaker + hall, and
/// a bottom row with a state chip (مباشر الآن live / مسجّل recorded — the summary
/// chip is suppressed since the whole list is summarised) + the category chip.
/// Tapping it opens that session's AI-summary details (#34).
class SessionSummaryCard extends StatelessWidget {
  const SessionSummaryCard({
    required this.item,
    required this.isArabic,
    required this.l10n,
    required this.durationLabel,
    super.key,
  });

  final SessionListItem item;
  final bool isArabic;
  final AppL10n l10n;
  final String durationLabel;

  @override
  Widget build(BuildContext context) {
    // Frame 1388:8439 — 24h "HH:mm" (e.g. 09:00), not the locale's 12h ص/م.
    final start = item.startLocal;
    final time = '${start.hour.toString().padLeft(2, '0')}:'
        '${start.minute.toString().padLeft(2, '0')}';
    final speaker = _speakerText();
    final hall = item.localizedHall(isArabic);
    final category = item.localizedCategory(isArabic);
    // Owner 2026-07-14 — the shared state chips. The summaries list is already
    // all-summarised, so the summary chip is suppressed here (redundant) and the
    // card shows live-now / مسجّل only.
    final stateChips = sessionStateChips(
      phase: item.phase(DateTime.now().toUtc()),
      hasPublishedSummary: false,
      status: item.status,
    );
    final hasChips = stateChips.isNotEmpty;

    final hasMeta = speaker != null || (hall != null && hall.isNotEmpty);
    return SimfCard(
      onTap: () => context.pushNamed(
        RouteNames.aiSummary,
        queryParameters: <String, String>{RouteParams.sessionId: item.id},
      ),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2), // p-8 (Figma 1388:8430)
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            // Title + time on the right, the favourite heart on the left.
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      Text(
                        item.localizedTitle(isArabic),
                        textAlign: TextAlign.start,
                        style: const TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.w500,
                          fontSize: SimfTokens.textMd, // 14
                        ),
                      ),
                      const SizedBox(height: SimfTokens.space2),
                      SessionIconLine(
                        // Figma 1388:8441 — the exact iconify clock, not Material.
                        asset: 'assets/icons/session_clock.svg',
                        text: '$time · $durationLabel',
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: SimfTokens.space2),
                FavouriteHeartButton(sessionId: item.id),
              ],
            ),
            // Speaker (right) + hall (left), each in a beige icon-box group.
            if (hasMeta) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: <Widget>[
                  if (speaker != null)
                    Flexible(
                      child: SessionMetaGroup(
                        // Figma 1388:8457 — the exact iconify "users" glyph.
                        asset: 'assets/icons/session_users.svg',
                        text: speaker,
                      ),
                    ),
                  if (hall != null && hall.isNotEmpty)
                    Flexible(
                      child: SessionMetaGroup(
                        // Figma 1388:8449 — the exact iconify map-pin glyph.
                        asset: 'assets/icons/session_location.svg',
                        text: hall,
                      ),
                    ),
                ],
              ),
            ],
            if (hasChips ||
                (category != null && category.isNotEmpty)) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              // The state chip sits on the inline end (left in RTL), the category
              // pill filling the rest (Figma 1388:8462).
              Row(
                children: <Widget>[
                  if (category != null && category.isNotEmpty) ...<Widget>[
                    Expanded(child: _CategoryPill(label: category)),
                    if (hasChips) const SizedBox(width: SimfTokens.space4),
                  ],
                  if (hasChips) SessionStateChipRow(kinds: stateChips, l10n: l10n),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  String? _speakerText() {
    if (item.speakers.isEmpty) {
      return null;
    }
    final primary = item.speakers.first;
    final name = primary.localizedName(isArabic);
    final title = primary.title?.trim();
    return title == null || title.isEmpty ? name : '$name · $title';
  }
}

/// The bordered category pill on the card's bottom row.
class _CategoryPill extends StatelessWidget {
  const _CategoryPill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4, // 16
        vertical: SimfTokens.space2, // 8
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius), // 8
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Text(
        label,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        textAlign: TextAlign.center,
        style: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textSm, // 12
          fontWeight: FontWeight.w500,
        ),
      ),
    );
  }
}
