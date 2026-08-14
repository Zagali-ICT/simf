import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/ai_summary/widgets/category_pill.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/favourite_heart_button.dart';
import 'package:simf_app/features/sessions/widgets/session_card_meta.dart';
import 'package:simf_app/features/sessions/widgets/session_state_chip.dart';

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
    // Frame 1388:8439 — 12h "hh:mm a" (e.g. 09:00 AM) on the Saudi wall clock.
    final time = formatSaudiTime12(item.startLocal);
    final speaker = _speakerText();
    final hall = item.localizedHall(isArabic: isArabic);
    final category = item.localizedCategory(isArabic: isArabic);
    // Owner 2026-07-14 — the shared state chips. The summaries list is already
    // all-summarised, so the summary chip is suppressed here (redundant) and
    // the card shows live-now / مسجّل only.
    final stateChips = sessionStateChips(
      phase: item.phase(saudiNow()),
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
        padding:
            const EdgeInsets.all(SimfTokens.space2), // p-8 (Figma 1388:8430)
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
                        item.localizedTitle(isArabic: isArabic),
                        textAlign: TextAlign.start,
                        style: SimfTokens.labelWhiteMedium,
                      ),
                      const SizedBox(height: SimfTokens.space2),
                      SessionIconLine(
                        // Figma 1388:8441 — the exact iconify clock, not
                        // Material.
                        asset: AppAssets.sessionClock,
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
                        asset: AppAssets.sessionUsers,
                        text: speaker,
                      ),
                    ),
                  if (hall != null && hall.isNotEmpty)
                    Flexible(
                      child: SessionMetaGroup(
                        // Figma 1388:8449 — the exact iconify map-pin glyph.
                        asset: AppAssets.sessionLocation,
                        text: hall,
                      ),
                    ),
                ],
              ),
            ],
            if (hasChips ||
                (category != null && category.isNotEmpty)) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              // The state chip sits on the inline end (left in RTL), the
              // category pill filling the rest (Figma 1388:8462).
              Row(
                children: <Widget>[
                  if (category != null && category.isNotEmpty) ...<Widget>[
                    Expanded(child: CategoryPill(label: category)),
                    if (hasChips) const SizedBox(width: SimfTokens.space4),
                  ],
                  if (hasChips)
                    SessionStateChipRow(kinds: stateChips, l10n: l10n),
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
    final name = primary.localizedName(isArabic: isArabic);
    final title = primary.title?.trim();
    return title == null || title.isEmpty ? name : '$name · $title';
  }
}
