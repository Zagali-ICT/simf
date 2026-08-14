import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_cards.dart';
import 'package:simf_app/app/widgets/simf_states.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/myarea/data/my_sessions_models.dart';
import 'package:simf_app/features/sessions/data/session_lifecycle.dart';
import 'package:simf_app/features/sessions/widgets/favourite_heart_button.dart';
import 'package:simf_app/features/sessions/widgets/session_card_meta.dart';
import 'package:simf_app/features/sessions/widgets/session_state_chip.dart';

class MySessionsTabbedList extends StatelessWidget {
  const MySessionsTabbedList({
    required this.items,
    required this.tabLabel,
    required this.l10n,
    super.key,
  });

  final List<MyAreaSessionItem> items;
  final String tabLabel;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    if (items.isEmpty) {
      return ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        children: <Widget>[
          SimfEmptyState(
            icon: Icons.event_note_outlined,
            message: l10n.mySessionsEmpty,
          ),
        ],
      );
    }
    final isArabic = l10n.isArabic;
    return ListView.separated(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        0,
        SimfTokens.space4,
        SimfTokens.space5,
      ),
      itemCount: items.length + 1,
      separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space3),
      itemBuilder: (context, index) {
        if (index == 0) {
          return Padding(
            padding: const EdgeInsets.only(bottom: SimfTokens.space2),
            child: Text(
              l10n.mySessionsCount(items.length, tabLabel),
              style: const TextStyle(
                color: SimfTokens.surface,
                fontSize: SimfTokens.textLg, // 16
                fontWeight: FontWeight.w500,
              ),
            ),
          );
        }
        return MySessionCard(item: items[index - 1], isArabic: isArabic);
      },
    );
  }
}

/// One my-session card: the heart on the trailing edge, the title over a
/// clock·time line with the category chip, and the primary speaker + hall.
class MySessionCard extends StatelessWidget {
  const MySessionCard({required this.item, required this.isArabic, super.key});

  final MyAreaSessionItem item;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final time = TimeOfDay.fromDateTime(item.startLocal).format(context);
    final category = item.localizedCategory(isArabic: isArabic);
    final speaker = item.localizedSpeaker(isArabic: isArabic);
    final hall = item.localizedHall(isArabic: isArabic);
    final timeText =
        (category != null && category.isNotEmpty) ? '$time · $category' : time;
    final hasMeta = speaker != null || (hall != null && hall.isNotEmpty);
    // Owner 2026-07-14 — the same state chips as the agenda (my-sessions
    // carries no summary flag, so only live-now / recorded show here).
    final phase = sessionPhase(item.start, item.end, saudiNow());
    final stateChips = sessionStateChips(
      phase: phase,
      hasPublishedSummary: false,
      status: item.status,
    );

    return SimfCard(
      onTap: () => context.pushNamed(
        RouteNames.sessionDetail,
        pathParameters: <String, String>{RouteParams.sessionId: item.id},
      ),
      child: Padding(
        padding:
            const EdgeInsets.all(SimfTokens.space2), // p-8 (Figma 1388:9115)
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            // Title + time·category on the right, the favourite heart on the
            // left.
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
                        style: const TextStyle(
                          color: SimfTokens.surface,
                          fontWeight: FontWeight.w500,
                          fontSize: SimfTokens.textMd, // 14
                        ),
                      ),
                      const SizedBox(height: SimfTokens.space2),
                      SessionIconLine(icon: Icons.access_time, text: timeText),
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
                        icon: Icons.person_outline,
                        text: _speakerText(speaker),
                      ),
                    ),
                  if (hall != null && hall.isNotEmpty)
                    Flexible(
                      child: SessionMetaGroup(
                        icon: Icons.location_on_outlined,
                        text: hall,
                      ),
                    ),
                ],
              ),
            ],
            if (stateChips.isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space3),
              SessionStateChipRow(kinds: stateChips, l10n: AppL10n.of(context)),
            ],
          ],
        ),
      ),
    );
  }

  String _speakerText(String speaker) {
    final title = item.speakerTitle?.trim();
    return title == null || title.isEmpty ? speaker : '$speaker · $title';
  }
}
