import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/request_models.dart';
import 'data/requests_repository.dart';
import 'new_request_sheet.dart';

/// D-500 (Wave 5, الطلبات 1408:9726) — the unified requests feed. A "طلب جديد"
/// action plus status filter chips (with counts), over expandable cards across
/// every request kind the user submitted (speaker / delegation / session
/// attendance / participation-document / badge-update). Supersedes the read-only
/// My-meetings screen. The user can cancel their own pending speaker / document
/// / badge requests.
class RequestsScreen extends ConsumerStatefulWidget {
  const RequestsScreen({super.key});

  @override
  ConsumerState<RequestsScreen> createState() => _RequestsScreenState();
}

class _RequestsScreenState extends ConsumerState<RequestsScreen> {
  AppRequestStatus? _filter;

  void _toast(String message) {
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  Future<void> _openNewRequest() async {
    final submitted = await showNewRequestSheet(context);
    if (submitted && mounted) {
      ref.invalidate(myRequestsProvider);
      _toast(AppL10n.of(context).requestSubmitted);
    }
  }

  Future<void> _cancel(AppRequestItem item) async {
    final l10n = AppL10n.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: SimfTokens.navyDeep,
        title: Text(
          l10n.requestCancelConfirm,
          style: const TextStyle(color: Colors.white),
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: Text(l10n.requestCancelKeep),
          ),
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(true),
            child: Text(
              l10n.requestCancel,
              style: const TextStyle(color: SimfTokens.danger),
            ),
          ),
        ],
      ),
    );
    if (confirmed != true) {
      return;
    }
    try {
      await ref
          .read(requestsRepositoryProvider)
          .cancelRequest(kind: item.kind, id: item.id);
      if (!mounted) {
        return;
      }
      ref.invalidate(myRequestsProvider);
      _toast(l10n.requestCancelled);
    } on ApiFailure {
      _toast(l10n.requestCancelFailed);
    }
  }

  /// Pull-to-refresh — re-fetch the requests feed (invalidate + await next).
  Future<void> _refresh() async {
    ref.invalidate(myRequestsProvider);
    await ref.read(myRequestsProvider.future);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.requestsTitle,
      onBack: () => backOrHome(context),
      body: ref.watch(myRequestsProvider).when(
            loading: () => const Center(child: CircularProgressIndicator()),
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
    // A selected status whose chip has dropped to zero items (e.g. the user just
    // cancelled their only pending request) falls back to "All" so the screen
    // never strands the user on a chip-less "no results" view.
    final effectiveFilter =
        (_filter != null && items.any((i) => i.status == _filter))
            ? _filter
            : null;
    final filtered = effectiveFilter == null
        ? items
        : items.where((i) => i.status == effectiveFilter).toList();

    return SimfPullToRefresh(
      onRefresh: _refresh,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          _TopActionRow(
          l10n: l10n,
          filter: effectiveFilter,
          // المقبولة can only filter when at least one accepted request exists
          // (else the zero-count fallback would make it a silent dead-tap).
          acceptedEnabled:
              items.any((i) => i.status == AppRequestStatus.accepted),
          onNew: () => unawaited(_openNewRequest()),
          onSelect: (status) => setState(() => _filter = status),
        ),
        const SizedBox(height: SimfTokens.space4),
        if (items.isNotEmpty)
          _StatusChips(
            items: items,
            selected: effectiveFilter,
            l10n: l10n,
            onSelect: (status) => setState(() => _filter = status),
          ),
        const SizedBox(height: SimfTokens.space4),
        if (items.isEmpty)
          SimfEmptyState(
            icon: Icons.inbox_outlined,
            message: l10n.requestsEmpty,
          )
        else if (filtered.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: SimfTokens.space6),
            child: Center(
              child: Text(
                l10n.requestsNoResults,
                style: const TextStyle(color: SimfTokens.beigeBorder),
              ),
            ),
          )
        else
          for (final item in filtered)
            Padding(
              padding: const EdgeInsets.only(bottom: SimfTokens.space3),
              // Key on kind+id so the card's expanded state follows the request
              // identity, not its list position, across a cancel/refetch.
              child: _RequestCard(
                key: ValueKey<String>('${item.kind.wireValue}:${item.id}'),
                item: item,
                isArabic: isArabic,
                l10n: l10n,
                onCancel: () => unawaited(_cancel(item)),
              ),
            ),
        ],
      ),
    );
  }
}

/// The top action row (Figma 1408:9736): three equal buttons — "طلب جديد"
/// (opens the new-request sheet), "المقبولة" (filter → accepted) and "السجل"
/// (filter → all). The active filter button is gold-filled (white text), the
/// rest are beige-outlined. "طلب جديد" is an action, never the active state.
class _TopActionRow extends StatelessWidget {
  const _TopActionRow({
    required this.l10n,
    required this.filter,
    required this.acceptedEnabled,
    required this.onNew,
    required this.onSelect,
  });

  final AppL10n l10n;
  final AppRequestStatus? filter;
  final bool acceptedEnabled;
  final VoidCallback onNew;
  final ValueChanged<AppRequestStatus?> onSelect;

  @override
  Widget build(BuildContext context) {
    // The Figma lays this row left→right (طلب جديد · المقبولة · السجل) — a fixed
    // LTR control row in the mock — so force LTR to match it exactly rather than
    // letting the RTL shell mirror it.
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Row(
        children: <Widget>[
          Expanded(
            child: _ActionButton(
              label: l10n.requestNew,
              icon: Icons.note_add_outlined,
              active: false,
              onTap: onNew,
            ),
          ),
          const SizedBox(width: SimfTokens.space4),
          Expanded(
            child: _ActionButton(
              label: l10n.requestsTabAccepted,
              icon: Icons.thumb_up_outlined,
              active: filter == AppRequestStatus.accepted,
              enabled: acceptedEnabled,
              onTap: () => onSelect(AppRequestStatus.accepted),
            ),
          ),
          const SizedBox(width: SimfTokens.space4),
          Expanded(
            child: _ActionButton(
              label: l10n.requestsTabLog,
              icon: Icons.history,
              active: filter == null,
              onTap: () => onSelect(null),
            ),
          ),
        ],
      ),
    );
  }
}

/// One equal-width pill in the [_TopActionRow]: gold-filled when [active], else
/// a beige-hairline outline. Label SemiBold-12 over a 14px leading icon.
class _ActionButton extends StatelessWidget {
  const _ActionButton({
    required this.label,
    required this.icon,
    required this.active,
    required this.onTap,
    this.enabled = true,
  });

  final String label;
  final IconData icon;
  final bool active;
  final VoidCallback onTap;

  /// false → dimmed, not tappable (e.g. المقبولة with no accepted requests).
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final Color base = active ? Colors.white : SimfTokens.beigeBorder;
    final Color fg = enabled ? base : base.withValues(alpha: 0.4);
    final Color borderColor =
        enabled ? SimfTokens.beigeBorder : SimfTokens.beigeBorder.withValues(alpha: 0.4);
    return InkWell(
      onTap: enabled ? onTap : null,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Container(
        padding: const EdgeInsets.all(SimfTokens.space2),
        decoration: BoxDecoration(
          color: active ? SimfTokens.accent : Colors.transparent,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          border: active
              ? null
              : Border.all(
                  color: borderColor,
                  width: SimfTokens.hairline,
                ),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Flexible(
              child: Text(
                label,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: fg,
                  fontSize: SimfTokens.textSm, // 12
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space1),
            Icon(icon, size: 14, color: fg),
          ],
        ),
      ),
    );
  }
}

/// The horizontally-scrolling status filter chips — each populated status with
/// its count (no "All" chip; السجل in the top row serves "all"). Figma chip row.
class _StatusChips extends StatelessWidget {
  const _StatusChips({
    required this.items,
    required this.selected,
    required this.l10n,
    required this.onSelect,
  });

  final List<AppRequestItem> items;
  final AppRequestStatus? selected;
  final AppL10n l10n;
  final ValueChanged<AppRequestStatus?> onSelect;

  @override
  Widget build(BuildContext context) {
    final chips = <Widget>[];
    for (final status in _chipOrder) {
      final count = items.where((i) => i.status == status).length;
      if (count == 0) {
        continue;
      }
      if (chips.isNotEmpty) {
        chips.add(const SizedBox(width: SimfTokens.space4));
      }
      chips.add(
        _chip(label: _statusLabel(l10n, status), count: count, status: status),
      );
    }
    // The Figma lays the chips left→right (ملغى … مقبول) — match that order.
    return Directionality(
      textDirection: TextDirection.ltr,
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(children: chips),
      ),
    );
  }

  /// A status pill (Figma 1408:9761+): the status colour at 12% fill + 20%
  /// border, radius-4, h-32; the colour at full strength for the text. Tapping
  /// toggles the filter (a stronger fill marks the selected chip).
  Widget _chip({
    required String label,
    required int count,
    required AppRequestStatus status,
  }) {
    final color = _statusColor(status);
    final active = selected == status;
    return InkWell(
      onTap: () => onSelect(active ? null : status),
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: Container(
        height: 32,
        alignment: Alignment.center,
        padding: const EdgeInsets.symmetric(horizontal: 13),
        decoration: BoxDecoration(
          color: color.withValues(alpha: active ? 0.24 : 0.12),
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          border: Border.all(
            color: color.withValues(alpha: active ? 1 : 0.2),
          ),
        ),
        child: Text(
          '$label ($count)',
          style: TextStyle(
            color: color,
            fontSize: SimfTokens.textSm, // 12
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
    );
  }
}

String _statusLabel(AppL10n l10n, AppRequestStatus status) {
  switch (status) {
    case AppRequestStatus.accepted:
      return l10n.requestStatusAccepted;
    case AppRequestStatus.rejected:
      return l10n.requestStatusRejected;
    case AppRequestStatus.cancelled:
      return l10n.requestStatusCancelled;
    case AppRequestStatus.pending:
      return l10n.requestStatusPending;
  }
}

Color _statusColor(AppRequestStatus status) {
  switch (status) {
    case AppRequestStatus.accepted:
      return SimfTokens.statusAccepted;
    case AppRequestStatus.rejected:
      return SimfTokens.statusRejected;
    case AppRequestStatus.cancelled:
      return SimfTokens.statusCancelled;
    case AppRequestStatus.pending:
      return SimfTokens.qStage; // amber #F59E0B (Figma قيد المراجعة)
  }
}

/// The display order of the status chips (Figma 1408:9760): cancelled, rejected,
/// pending, accepted — only those with at least one request are shown.
const List<AppRequestStatus> _chipOrder = <AppRequestStatus>[
  AppRequestStatus.cancelled,
  AppRequestStatus.rejected,
  AppRequestStatus.pending,
  AppRequestStatus.accepted,
];

String _kindHeadline(AppL10n l10n, AppRequestKind kind) {
  switch (kind) {
    case AppRequestKind.delegationMeeting:
      return l10n.requestKindDelegation;
    case AppRequestKind.sessionAttendance:
      return l10n.requestKindSession;
    case AppRequestKind.participationDocument:
      return l10n.requestKindDocument;
    case AppRequestKind.badgeUpdate:
      return l10n.requestKindBadge;
    case AppRequestKind.speakerMeeting:
      return l10n.requestKindSpeaker;
  }
}

IconData _kindIcon(AppRequestKind kind) {
  switch (kind) {
    case AppRequestKind.delegationMeeting:
      return Icons.flag_outlined;
    case AppRequestKind.sessionAttendance:
      return Icons.event_seat_outlined;
    case AppRequestKind.participationDocument:
      return Icons.description_outlined;
    case AppRequestKind.badgeUpdate:
      return Icons.badge_outlined;
    case AppRequestKind.speakerMeeting:
      return Icons.person_outline;
  }
}

/// One expandable request card: the type icon, headline + context line + date,
/// a status-coloured leading strip, and (when expanded) the status detail and a
/// cancel action for the user's own pending requests.
class _RequestCard extends StatefulWidget {
  const _RequestCard({
    super.key,
    required this.item,
    required this.isArabic,
    required this.l10n,
    required this.onCancel,
  });

  final AppRequestItem item;
  final bool isArabic;
  final AppL10n l10n;
  final VoidCallback onCancel;

  @override
  State<_RequestCard> createState() => _RequestCardState();
}

class _RequestCardState extends State<_RequestCard> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    final item = widget.item;
    final l10n = widget.l10n;
    final statusColor = _statusColor(item.status);
    final subtitle = item.localizedSubtitle(widget.isArabic);

    return Container(
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        // Full status-coloured hairline (Figma 1408:9773 — 0.5px all round).
        border: Border.all(color: statusColor, width: SimfTokens.hairlineBold),
      ),
      child: Column(
        children: <Widget>[
          InkWell(
            onTap: () => setState(() => _expanded = !_expanded),
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            child: Padding(
              padding: const EdgeInsets.all(SimfTokens.space2),
              child: Row(
                children: <Widget>[
                  _IconBox(icon: _kindIcon(item.kind)),
                  const SizedBox(width: SimfTokens.space2),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: <Widget>[
                        Text(
                          _kindHeadline(l10n, item.kind),
                          textAlign: TextAlign.start,
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: SimfTokens.textMd, // 14
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                        if (subtitle.isNotEmpty) ...<Widget>[
                          const SizedBox(height: SimfTokens.space2),
                          Text(
                            subtitle,
                            textAlign: TextAlign.start,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: SimfTokens.beigeBorder,
                              fontSize: SimfTokens.textSm, // 12
                            ),
                          ),
                        ],
                        const SizedBox(height: SimfTokens.space2),
                        Text(
                          l10n.requestDate(item.displayDate.toLocal()),
                          // Pinned LTR + the LRM in requestDate keep the date as
                          // "12 يناير 2026" (Figma 1408:9782); align to the
                          // trailing edge under the right-aligned title.
                          textDirection: TextDirection.ltr,
                          textAlign: TextAlign.end,
                          style: const TextStyle(
                            color: SimfTokens.beigeBorder,
                            fontSize: SimfTokens.textXs, // ~10
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: SimfTokens.space2),
                  Icon(
                    _expanded
                        ? Icons.keyboard_arrow_up
                        : Icons.keyboard_arrow_down,
                    size: 20,
                    color: SimfTokens.beigeBorder,
                  ),
                ],
              ),
            ),
          ),
          if (_expanded) _buildDetail(l10n, item, statusColor),
        ],
      ),
    );
  }

  Widget _buildDetail(AppL10n l10n, AppRequestItem item, Color statusColor) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space3,
        0,
        SimfTokens.space3,
        SimfTokens.space3,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const Divider(color: SimfTokens.line, height: SimfTokens.space4),
          Row(
            children: <Widget>[
              Container(
                width: 8,
                height: 8,
                decoration: BoxDecoration(color: statusColor, shape: BoxShape.circle),
              ),
              const SizedBox(width: SimfTokens.space2),
              Text(
                _statusLabel(l10n, item.status),
                style: TextStyle(
                  color: statusColor,
                  fontSize: SimfTokens.textSm,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
          if (item.canCancel) ...<Widget>[
            const SizedBox(height: SimfTokens.space3),
            Align(
              alignment: AlignmentDirectional.centerEnd,
              child: OutlinedButton.icon(
                onPressed: widget.onCancel,
                icon: const Icon(Icons.close, size: 16, color: SimfTokens.danger),
                label: Text(
                  l10n.requestCancel,
                  style: const TextStyle(color: SimfTokens.danger),
                ),
                style: OutlinedButton.styleFrom(
                  side: const BorderSide(color: SimfTokens.danger),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// The gold rounded type-icon box at the inline start of a card (Figma
/// 1408:9783 — 32px, radius-4, a 16px navy glyph).
class _IconBox extends StatelessWidget {
  const _IconBox({required this.icon});

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 32,
      height: 32,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Icon(icon, size: 16, color: SimfTokens.navy),
    );
  }
}
