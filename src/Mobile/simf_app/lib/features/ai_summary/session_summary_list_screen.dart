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
      title: l10n.aiSummaryTitle,
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
          ),
          const SizedBox(height: SimfTokens.space3),
          Expanded(
            child: sessions.when(
              loading: () => const Center(
                child: CircularProgressIndicator(color: SimfTokens.accent),
              ),
              error: (_, __) => KsaErrorState(
                message: l10n.aiSummaryError,
                retryLabel: l10n.retryLabel,
                onRetry: () => ref.invalidate(aiSummarySessionsProvider),
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
      return KsaEmptyState(
        icon: Icons.summarize_outlined,
        message: _emptyMessage(l10n, items.isEmpty),
      );
    }

    final days = _distinctDays(filtered);

    return ListView.builder(
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
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: SimfTokens.textLg,
                  fontWeight: FontWeight.w600,
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

/// The navy search field with a leading magnifier (Figma 1388:8392).
class _SearchField extends StatelessWidget {
  const _SearchField({required this.hint, required this.onChanged});

  final String hint;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return TextField(
      onChanged: onChanged,
      style: const TextStyle(color: Colors.white, fontSize: SimfTokens.textMd),
      decoration: InputDecoration(
        hintText: hint,
        hintStyle: const TextStyle(
          color: SimfTokens.beigeBorder,
          fontSize: SimfTokens.textMd,
        ),
        prefixIcon: const Icon(Icons.search, color: SimfTokens.beigeBorder),
        filled: true,
        fillColor: SimfTokens.navyDeep,
        contentPadding: const EdgeInsets.symmetric(
          vertical: SimfTokens.space3,
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(
            color: SimfTokens.beigeBorder,
            width: SimfTokens.hairline,
          ),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(
            color: SimfTokens.beigeBorder,
            width: SimfTokens.hairline,
          ),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(
            color: SimfTokens.accent,
            width: SimfTokens.hairlineBold,
          ),
        ),
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
    final time = TimeOfDay.fromDateTime(item.startLocal).format(context);
    final speaker = _speakerText();
    final hall = item.localizedHall(isArabic);
    final category = item.localizedCategory(isArabic);
    final isRecorded = item.status == SessionStatus.recorded ||
        item.status == SessionStatus.published;

    return KsaCard(
      onTap: () => context.pushNamed(
        RouteNames.aiSummary,
        queryParameters: <String, String>{'sessionId': item.id},
      ),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space3),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        item.localizedTitle(isArabic),
                        style: const TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.w600,
                          fontSize: SimfTokens.textLg,
                        ),
                      ),
                      const SizedBox(height: SimfTokens.space2),
                      _IconLine(
                        icon: Icons.access_time,
                        text: '$time · $durationLabel',
                      ),
                      if (speaker != null) ...<Widget>[
                        const SizedBox(height: SimfTokens.space1),
                        _IconLine(
                          icon: Icons.person_outline,
                          text: speaker,
                          trailing: hall,
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(width: SimfTokens.space3),
                FavouriteHeartButton(sessionId: item.id),
              ],
            ),
            if (isRecorded || (category != null && category.isNotEmpty)) ...[
              const SizedBox(height: SimfTokens.space3),
              Row(
                children: <Widget>[
                  if (isRecorded) ...<Widget>[
                    _RecordedBadge(label: recordedBadge),
                    const SizedBox(width: SimfTokens.space2),
                  ],
                  if (category != null && category.isNotEmpty)
                    Expanded(child: _CategoryPill(label: category)),
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

class _IconLine extends StatelessWidget {
  const _IconLine({required this.icon, required this.text, this.trailing});

  final IconData icon;
  final String text;
  final String? trailing;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Icon(icon, size: 14, color: SimfTokens.beigeBorder),
        const SizedBox(width: SimfTokens.space1),
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
        if (trailing != null && trailing!.isNotEmpty) ...<Widget>[
          const SizedBox(width: SimfTokens.space2),
          Text(
            trailing!,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ],
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
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(Icons.circle, size: 8, color: SimfTokens.navy),
          const SizedBox(width: SimfTokens.space1),
          Text(
            label,
            style: const TextStyle(
              color: SimfTokens.navy,
              fontSize: SimfTokens.textXs,
              fontWeight: FontWeight.w600,
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
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space2,
      ),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
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
          color: SimfTokens.beigeBorder,
          fontSize: SimfTokens.textXs,
        ),
      ),
    );
  }
}
