import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart' show DateFormat;

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/utils/gregorian_month_names.dart';
import 'data/request_models.dart';
import 'data/requests_repository.dart';

/// **My meetings** — App "المقابلات" (Figma 1701:9406, route `/my-meetings`,
/// approved attendee), reached from the My-Area "مقابلات" counter. The caller's
/// **speaker + delegation** meeting requests as person cards — a gold initial
/// avatar, the counterpart name, the meeting type, the slot date, and a status
/// badge — over the status filter chips (الكل always, plus مكتملة / قيد الانتظار /
/// مرفوضة only when their bucket is non-empty, matching Figma 1701:9406's
/// zero-rejected three-chip row).
///
/// Read-only: it reuses the الطلبات feed ([myRequestsProvider]) filtered to the
/// two meeting kinds — no new endpoint. **Cancelled** meetings are excluded (they
/// remain visible on الطلبات), so الكل equals accepted + pending + rejected and
/// every card is reachable by a chip.
class MyMeetingsScreen extends ConsumerStatefulWidget {
  const MyMeetingsScreen({super.key});

  @override
  ConsumerState<MyMeetingsScreen> createState() => _MyMeetingsScreenState();
}

/// The card time part ("10:30 PM"), hoisted off the build path.
final DateFormat _meetingTimeFormat = DateFormat('hh:mm a');

/// The status buckets the screen filters by (Figma chip row, RTL right→left):
/// الكل, مكتملة (accepted), قيد الانتظار (pending), مرفوضة (rejected).
enum _MeetingFilter { all, accepted, pending, rejected }

class _MyMeetingsScreenState extends ConsumerState<MyMeetingsScreen> {
  _MeetingFilter _filter = _MeetingFilter.all;

  /// Pull-to-refresh — re-fetch the requests feed (invalidate + await next).
  Future<void> _refresh() async {
    ref.invalidate(myRequestsProvider);
    await ref.read(myRequestsProvider.future);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.myMeetingsTitle,
      onBack: () => backOrHome(context),
      body: ref.watch(myRequestsProvider).when(
            loading: () => const Center(
              child: CircularProgressIndicator(color: SimfTokens.accent),
            ),
            error: (_, __) => SimfPullToRefresh(
              onRefresh: _refresh,
              child: SimfPullableHost(
                child: SimfErrorState(
                  message: l10n.requestsError,
                  retryLabel: l10n.retryLabel,
                  onRetry: () => ref.invalidate(myRequestsProvider),
                ),
              ),
            ),
            data: (items) => _buildBody(l10n, items),
          ),
    );
  }

  Widget _buildBody(AppL10n l10n, List<AppRequestItem> items) {
    final isArabic = l10n.isArabic;

    // Meetings only (speaker + delegation), excluding cancelled — those stay on
    // الطلبات. The feed is already newest-first.
    bool isMeeting(AppRequestItem i) =>
        (i.kind == AppRequestKind.speakerMeeting ||
            i.kind == AppRequestKind.delegationMeeting) &&
        i.status != AppRequestStatus.cancelled;
    final meetings = items.where(isMeeting).toList(growable: false);

    final counts = <_MeetingFilter, int>{
      _MeetingFilter.all: meetings.length,
      _MeetingFilter.accepted:
          meetings.where((m) => m.status == AppRequestStatus.accepted).length,
      _MeetingFilter.pending:
          meetings.where((m) => m.status == AppRequestStatus.pending).length,
      _MeetingFilter.rejected:
          meetings.where((m) => m.status == AppRequestStatus.rejected).length,
    };

    // A selected chip that has dropped to zero (e.g. after a refetch) falls back
    // to الكل so the user is never stranded on an empty filter.
    final effective = _filter != _MeetingFilter.all && (counts[_filter] ?? 0) == 0
        ? _MeetingFilter.all
        : _filter;
    final filtered = effective == _MeetingFilter.all
        ? meetings
        : meetings
            .where((m) => m.status == _statusFor(effective))
            .toList(growable: false);

    return SimfPullToRefresh(
      onRefresh: _refresh,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          _StatusFilterRow(
            counts: counts,
            selected: effective,
            l10n: l10n,
            onSelect: (f) => setState(() => _filter = f),
          ),
          const SizedBox(height: SimfTokens.space4),
          if (meetings.isEmpty)
            SimfEmptyState(
              icon: Icons.groups_outlined,
              message: l10n.myMeetingsEmpty,
            )
          else ...<Widget>[
            Text(
              // The total meeting count (not the filtered subset) — a stable
              // "جميع المقابلات (N)" section total that matches Figma 1701:9406
              // and never contradicts the active status chip below it.
              l10n.myMeetingsAllHeader(meetings.length),
              textAlign: TextAlign.start,
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textLg, // 16
                fontWeight: FontWeight.w500,
              ),
            ),
            const SizedBox(height: SimfTokens.space3),
            for (final m in filtered)
              Padding(
                padding: const EdgeInsets.only(bottom: SimfTokens.space3),
                child: _MeetingCard(item: m, isArabic: isArabic, l10n: l10n),
              ),
          ],
        ],
      ),
    );
  }
}

AppRequestStatus _statusFor(_MeetingFilter f) {
  switch (f) {
    case _MeetingFilter.accepted:
      return AppRequestStatus.accepted;
    case _MeetingFilter.pending:
      return AppRequestStatus.pending;
    case _MeetingFilter.rejected:
      return AppRequestStatus.rejected;
    case _MeetingFilter.all:
      return AppRequestStatus.accepted; // never queried for `all`
  }
}

/// The filter's chip colour (Figma 1701:9416+): الكل gold, مكتملة green,
/// قيد الانتظار amber, مرفوضة red.
Color _filterColor(_MeetingFilter f) {
  switch (f) {
    case _MeetingFilter.all:
      return SimfTokens.accent;
    case _MeetingFilter.accepted:
      return SimfTokens.statusAccepted;
    case _MeetingFilter.pending:
      return SimfTokens.qStage;
    case _MeetingFilter.rejected:
      return SimfTokens.statusRejected;
  }
}

String _filterLabel(AppL10n l10n, _MeetingFilter f) {
  switch (f) {
    case _MeetingFilter.all:
      return l10n.sessionTypeAll; // الكل
    case _MeetingFilter.accepted:
      return l10n.myMeetingsFilterCompleted; // مكتملة
    case _MeetingFilter.pending:
      return l10n.myMeetingsFilterPending; // قيد الانتظار
    case _MeetingFilter.rejected:
      return l10n.myMeetingsFilterRejected; // مرفوضة
  }
}

/// The status badge label on a card (Figma 1701:9446 — accepted shows مؤكدة).
String _badgeLabel(AppL10n l10n, AppRequestStatus status) {
  switch (status) {
    case AppRequestStatus.accepted:
      return l10n.myMeetingBadgeConfirmed; // مؤكدة
    case AppRequestStatus.pending:
      return l10n.myMeetingsFilterPending; // قيد الانتظار
    case AppRequestStatus.rejected:
      return l10n.myMeetingsFilterRejected; // مرفوضة
    case AppRequestStatus.cancelled:
      return l10n.myMeetingsFilterRejected; // excluded upstream; safe fallback
  }
}

/// The status filter chips (Figma 1701:9415). الكل always shows; مكتملة /
/// قيد الانتظار / مرفوضة appear only when their bucket is non-empty, so the
/// frame's zero-rejected sample reads as three chips (not four). Each visible
/// chip is equal-width: the filter colour at 12% fill + 20% border with the
/// colour as text; the selected chip is a solid colour fill with white text.
/// Laid start→end so الكل sits at the inline start (right, RTL).
class _StatusFilterRow extends StatelessWidget {
  const _StatusFilterRow({
    required this.counts,
    required this.selected,
    required this.l10n,
    required this.onSelect,
  });

  final Map<_MeetingFilter, int> counts;
  final _MeetingFilter selected;
  final AppL10n l10n;
  final ValueChanged<_MeetingFilter> onSelect;

  @override
  Widget build(BuildContext context) {
    const order = <_MeetingFilter>[
      _MeetingFilter.all,
      _MeetingFilter.accepted,
      _MeetingFilter.pending,
      _MeetingFilter.rejected,
    ];
    // الكل always shows; a status bucket appears only when it has meetings, so
    // the zero-rejected frame reads as three chips and the row never overflows
    // (Figma 1701:9406).
    final visible = <_MeetingFilter>[
      for (final f in order)
        if (f == _MeetingFilter.all || (counts[f] ?? 0) > 0) f,
    ];
    final chips = <Widget>[];
    for (var i = 0; i < visible.length; i++) {
      if (i > 0) {
        chips.add(const SizedBox(width: SimfTokens.space2));
      }
      chips.add(
        Expanded(
          child: _FilterChip(
            label: _filterLabel(l10n, visible[i]),
            count: counts[visible[i]] ?? 0,
            color: _filterColor(visible[i]),
            active: selected == visible[i],
            onTap: () => onSelect(visible[i]),
          ),
        ),
      );
    }
    return Row(children: chips);
  }
}

/// One equal-width status filter chip: "{label} ({count})", h40, radius-4.
class _FilterChip extends StatelessWidget {
  const _FilterChip({
    required this.label,
    required this.count,
    required this.color,
    required this.active,
    required this.onTap,
  });

  final String label;
  final int count;
  final Color color;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: SimfTokens.borderRadiusSmall,
      child: Container(
        height: 40,
        alignment: Alignment.center,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
        decoration: BoxDecoration(
          color: active ? color : color.withValues(alpha: 0.12),
          borderRadius: SimfTokens.borderRadiusSmall,
          border: Border.all(
            color: active ? color : color.withValues(alpha: 0.2),
            width: SimfTokens.hairline,
          ),
        ),
        child: Text(
          '$label ($count)',
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            color: active ? Colors.white : color,
            fontSize: SimfTokens.textSm, // 12
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
    );
  }
}

/// One meeting card (Figma 1701:9429): a gold initial avatar at the inline end
/// (right, RTL) with the counterpart name + meeting type beside it, and a date
/// line (clock + "12 يناير – 10:30 PM") opposite a neutral status badge.
class _MeetingCard extends StatelessWidget {
  const _MeetingCard({
    required this.item,
    required this.isArabic,
    required this.l10n,
  });

  final AppRequestItem item;
  final bool isArabic;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final name = item.localizedSubtitle(isArabic); // speaker / country name
    // Secondary line (Figma 1701:9406): the speaker's title/rank when the feed
    // carries one, else the meeting-type headline (delegations have no rank).
    final kind = item.kind == AppRequestKind.delegationMeeting
        ? l10n.requestKindDelegation
        : l10n.requestKindSpeaker;
    final subtitle = item.subtitle?.trim() ?? '';
    final secondary = subtitle.isNotEmpty ? subtitle : kind;
    final start = item.displayDate.toLocal();
    final date =
        '${start.day} ${gregorianMonthName(start.month, isArabic)} – ${_meetingTimeFormat.format(start)}';

    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4), // p-16
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep, // #192B41
        borderRadius: BorderRadius.circular(SimfTokens.radius), // 8
      ),
      child: Column(
        children: <Widget>[
          // Row 1 — avatar (inline end/right) + name over meeting type.
          Row(
            children: <Widget>[
              _InitialAvatar(name: name),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      name,
                      textAlign: TextAlign.start,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      secondary,
                      textAlign: TextAlign.start,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: SimfTokens.beigeBorder,
                        fontSize: SimfTokens.textSm, // 12
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space4),
          // Row 2 — status badge (inline start/right) opposite the date line.
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: <Widget>[
              _StatusBadge(label: _badgeLabel(l10n, item.status)),
              _DateLine(date: date),
            ],
          ),
        ],
      ),
    );
  }
}

/// The 38px gold square avatar with the counterpart's first letter (Figma
/// 1701:9435).
class _InitialAvatar extends StatelessWidget {
  const _InitialAvatar({required this.name});

  final String name;

  @override
  Widget build(BuildContext context) {
    final trimmed = name.trim();
    // Grapheme-safe first letter (matches the app's avatar-initial pattern)
    // so an astral-plane name never shows tofu.
    final initial = trimmed.isEmpty ? '؟' : trimmed.characters.first;
    return Container(
      width: 38,
      height: 38,
      alignment: Alignment.center,
      decoration: const BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: SimfTokens.borderRadiusSmall,
      ),
      child: Text(
        initial,
        style: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textSm, // 12
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

/// The clock + "12 يناير – 10:30 PM" date line (Figma 1701:9438). The date text
/// is pinned LTR so the day/month/time keep the mock's reading order.
class _DateLine extends StatelessWidget {
  const _DateLine({required this.date});

  final String date;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        const Icon(
          Icons.access_time,
          size: 12,
          color: SimfTokens.beigeBorder,
        ),
        const SizedBox(width: SimfTokens.space1),
        Text(
          date,
          textDirection: TextDirection.ltr,
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textXs, // ~10
            fontWeight: FontWeight.w500,
          ),
        ),
      ],
    );
  }
}

/// The neutral status badge (Figma 1701:9444): beige-10% fill, radius-4, p-8,
/// SemiBold-11 beige text.
class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2), // p-8
      decoration: const BoxDecoration(
        color: SimfTokens.beigeFill10, // rgba(194,184,162,0.10)
        borderRadius: SimfTokens.borderRadiusSmall,
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: SimfTokens.beigeBorder,
          fontSize: 11,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
