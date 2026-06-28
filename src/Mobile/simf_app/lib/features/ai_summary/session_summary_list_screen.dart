import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../myarea/data/my_sessions_repository.dart';
import '../sessions/data/session_favourites.dart';
import '../sessions/data/session_models.dart';
import '../sessions/widgets/favourite_heart_button.dart';
import '../sessions/widgets/session_filter_tabs.dart';
import 'session_summary_screen.dart' show aiSummarySessionsProvider;

/// **Session summaries** — App "ملخص الجلسات" (Figma 1388:8392, Guest+). Every
/// programme session in a searchable, day-grouped list with three tabs —
/// الجميع (all), جلساتي (the caller's booked sessions), المفضلة (favourited) —
/// and the المفضلة heart on each card. Tapping a card opens that session's
/// AI-summary details (#34). Reuses the cached programme (`aiSummarySessionsProvider`);
/// the booked set + favourites come from the approved-account reads (empty for a
/// guest).
class SessionSummaryListScreen extends ConsumerStatefulWidget {
  const SessionSummaryListScreen({super.key});

  @override
  ConsumerState<SessionSummaryListScreen> createState() =>
      _SessionSummaryListScreenState();
}

enum _SummaryTab { all, mine, favourites }

class _SessionSummaryListScreenState
    extends ConsumerState<SessionSummaryListScreen> {
  _SummaryTab _tab = _SummaryTab.all;
  String _query = '';

  /// Pull-to-refresh — re-fetch the programme (invalidate + await next).
  Future<void> _refresh() async {
    ref.invalidate(aiSummarySessionsProvider);
    await ref.read(aiSummarySessionsProvider.future);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final sessions = ref.watch(aiSummarySessionsProvider);

    final tabLabels = <String>[
      l10n.sessionsTabAll,
      l10n.sessionsTabMine,
      l10n.sessionsTabFavourites,
    ];

    return KsaPage(
      title: l10n.sessionSummariesTitle,
      onBack: () => ksaBackOrHome(context),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const SizedBox(height: SimfTokens.space3),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
            child: _SearchField(
              hint: l10n.sessionSummarySearchHint,
              onChanged: (value) => setState(() => _query = value),
            ),
          ),
          const SizedBox(height: SimfTokens.space3),
          SessionFilterTabs(
            labels: tabLabels,
            selectedIndex: _SummaryTab.values.indexOf(_tab),
            onSelected: (i) => setState(() => _tab = _SummaryTab.values[i]),
            // The summaries frame (1388:8392) has exactly 3 equal-width tabs.
            equalWidth: true,
          ),
          const SizedBox(height: SimfTokens.space3),
          Expanded(
            child: sessions.when(
              loading: () => const Center(
                child: CircularProgressIndicator(color: SimfTokens.accent),
              ),
              error: (_, __) => KsaRefresh(
                onRefresh: _refresh,
                child: KsaPullable(
                  child: KsaErrorState(
                    message: l10n.aiSummaryError,
                    retryLabel: l10n.retryLabel,
                    onRetry: () => ref.invalidate(aiSummarySessionsProvider),
                  ),
                ),
              ),
              data: (items) => _buildList(context, l10n, items),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildList(
    BuildContext context,
    AppL10n l10n,
    List<SessionListItem> items,
  ) {
    // Re-run the build (and so the filter + hearts) when the per-user sets
    // resolve or change, keeping the جلساتي / المفضلة tabs live.
    ref.watch(sessionFavouritesProvider);
    ref.watch(mySessionsProvider);

    final isArabic = l10n.isArabic;
    final filtered = _filter(items);
    if (filtered.isEmpty) {
      return KsaRefresh(
        onRefresh: _refresh,
        child: KsaPullable(
          child: KsaEmptyState(
            icon: Icons.summarize_outlined,
            message: _emptyMessage(l10n, items.isEmpty),
          ),
        ),
      );
    }

    final days = _distinctDays(filtered);

    return KsaRefresh(
      onRefresh: _refresh,
      child: ListView.builder(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          0,
          SimfTokens.space4,
          SimfTokens.space5,
        ),
        itemCount: days.length,
      itemBuilder: (context, dayIndex) {
        final day = days[dayIndex];
        final dayItems = filtered
            .where((s) => _sameDay(s.startLocal, day))
            .toList(growable: false);
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Padding(
              padding: const EdgeInsets.only(
                top: SimfTokens.space2,
                bottom: SimfTokens.space2,
              ),
              child: Text(
                l10n.eventDayLabel(dayIndex + 1),
                // Frame 1388:8428 — day header is Inter Medium (w500), not w600.
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: SimfTokens.textLg,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ),
            for (final item in dayItems)
              Padding(
                padding: const EdgeInsets.only(bottom: SimfTokens.space3),
                child: _SummaryCard(
                  item: item,
                  isArabic: isArabic,
                  recordedBadge: l10n.sessionRecordedBadge,
                  durationLabel: l10n.sessionDurationMinutes(
                    _durationMinutes(item),
                  ),
                ),
              ),
          ],
        );
        },
      ),
    );
  }

  List<SessionListItem> _filter(List<SessionListItem> items) {
    final favouriteIds =
        ref.read(sessionFavouritesProvider).valueOrNull ?? const <String>{};
    final mineIds = ref.read(mySessionsProvider).valueOrNull?.items
            .map((s) => s.id)
            .toSet() ??
        const <String>{};
    final needle = _query.trim().toLowerCase();

    return items.where((session) {
      switch (_tab) {
        case _SummaryTab.mine:
          if (!mineIds.contains(session.id)) {
            return false;
          }
        case _SummaryTab.favourites:
          if (!favouriteIds.contains(session.id)) {
            return false;
          }
        case _SummaryTab.all:
          break;
      }
      if (needle.isEmpty) {
        return true;
      }
      final haystack = <String?>[
        session.title,
        session.titleArabic,
        for (final speaker in session.speakers) speaker.name,
        for (final speaker in session.speakers) speaker.nameArabic,
      ].whereType<String>().join(' ').toLowerCase();
      return haystack.contains(needle);
    }).toList(growable: false);
  }

  String _emptyMessage(AppL10n l10n, bool noSessionsAtAll) {
    if (noSessionsAtAll) {
      return l10n.aiSummaryNoSessions;
    }
    if (_query.trim().isNotEmpty) {
      return l10n.sessionsNoMatch;
    }
    switch (_tab) {
      case _SummaryTab.mine:
        return l10n.sessionsNoMine;
      case _SummaryTab.favourites:
        return l10n.sessionsNoFavourites;
      case _SummaryTab.all:
        return l10n.aiSummaryNoSessions;
    }
  }

  List<DateTime> _distinctDays(List<SessionListItem> items) {
    final byKey = <String, DateTime>{};
    for (final s in items) {
      final local = s.startLocal;
      final key = '${local.year}-${local.month}-${local.day}';
      byKey.putIfAbsent(key, () => DateTime(local.year, local.month, local.day));
    }
    final days = byKey.values.toList()..sort();
    return days;
  }

  bool _sameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;

  int _durationMinutes(SessionListItem item) {
    final minutes = item.endUtc.difference(item.startUtc).inMinutes;
    return minutes < 0 ? 0 : minutes;
  }
}

/// The search field (Figma 1388:8402): the search glyph + hint at the inline
/// start (right in RTL), the filter glyph in a 40px divider-walled cell at the
/// inline end. radius-4, beige hairline, height 48. (Same shape as الوفود.)
class _SearchField extends StatelessWidget {
  const _SearchField({required this.hint, required this.onChanged});

  final String hint;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 48,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Row(
        children: <Widget>[
          const Padding(
            padding: EdgeInsetsDirectional.only(start: SimfTokens.space3),
            child: Icon(Icons.search, color: SimfTokens.beigeBorder, size: 16),
          ),
          Expanded(
            child: TextField(
              onChanged: onChanged,
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textMd,
              ),
              cursorColor: SimfTokens.accent,
              decoration: InputDecoration(
                hintText: hint,
                hintStyle: const TextStyle(
                  color: SimfTokens.beigeBorder,
                  fontSize: SimfTokens.textMd,
                ),
                isDense: true,
                border: InputBorder.none,
                contentPadding: const EdgeInsets.symmetric(
                  horizontal: SimfTokens.space3,
                  vertical: SimfTokens.space2,
                ),
              ),
            ),
          ),
          Container(
            width: 40,
            height: double.infinity,
            alignment: Alignment.center,
            decoration: const BoxDecoration(
              border: BorderDirectional(
                start: BorderSide(
                  color: SimfTokens.beigeBorder,
                  width: SimfTokens.hairline,
                ),
              ),
            ),
            child:
                const Icon(Icons.tune, color: SimfTokens.beigeBorder, size: 16),
          ),
        ],
      ),
    );
  }
}

/// One rich session-summary card (Figma 1388:8392): heart on the trailing edge,
/// the title over the clock·time·duration line, the primary speaker + hall, and
/// a bottom row with the مسجل badge (for recorded / published) + category chip.
class _SummaryCard extends StatelessWidget {
  const _SummaryCard({
    required this.item,
    required this.isArabic,
    required this.recordedBadge,
    required this.durationLabel,
  });

  final SessionListItem item;
  final bool isArabic;
  final String recordedBadge;
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
    final isRecorded = item.status == SessionStatus.recorded ||
        item.status == SessionStatus.published;

    final hasMeta = speaker != null || (hall != null && hall.isNotEmpty);
    return KsaCard(
      onTap: () => context.pushNamed(
        RouteNames.aiSummary,
        queryParameters: <String, String>{'sessionId': item.id},
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
                      _IconLine(
                        icon: Icons.access_time,
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
                      child: _MetaGroup(
                        icon: Icons.person_outline,
                        text: speaker,
                      ),
                    ),
                  if (hall != null && hall.isNotEmpty)
                    Flexible(
                      child: _MetaGroup(
                        icon: Icons.location_on_outlined,
                        text: hall,
                      ),
                    ),
                ],
              ),
            ],
            if (isRecorded || (category != null && category.isNotEmpty)) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              // مسجّل badge on the inline end (left in RTL), the category pill
              // filling the rest (Figma 1388:8462).
              Row(
                children: <Widget>[
                  if (category != null && category.isNotEmpty) ...<Widget>[
                    Expanded(child: _CategoryPill(label: category)),
                    if (isRecorded) const SizedBox(width: SimfTokens.space4),
                  ],
                  if (isRecorded) _RecordedBadge(label: recordedBadge),
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

/// The clock·time·duration line under the title (Figma 1388:8439): a 12px glyph
/// + a beige 12px label.
class _IconLine extends StatelessWidget {
  const _IconLine({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Icon(icon, size: 12, color: SimfTokens.beigeBorder),
        const SizedBox(width: SimfTokens.space2),
        Expanded(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ),
      ],
    );
  }
}

/// A speaker / hall meta group (Figma 1388:8445 / 8453): a 24px beige icon box
/// (navy glyph) leading (right in RTL), then the beige 12px label.
class _MetaGroup extends StatelessWidget {
  const _MetaGroup({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          width: 24,
          height: 24,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: SimfTokens.beigeBorder,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
          child: Icon(icon, size: 12, color: SimfTokens.navy),
        ),
        const SizedBox(width: SimfTokens.space2),
        Flexible(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ),
      ],
    );
  }
}

/// The gold "مسجل" (recorded) badge with a leading dot.
class _RecordedBadge extends StatelessWidget {
  const _RecordedBadge({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4, // 16
        vertical: SimfTokens.space2, // 8
      ),
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radius), // 8
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Text(
            label,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textSm, // 12
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Container(
            width: 8,
            height: 8,
            decoration: const BoxDecoration(
              color: Colors.white,
              shape: BoxShape.circle,
            ),
          ),
        ],
      ),
    );
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
