import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
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

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return KsaPage(
      title: l10n.requestsTitle,
      onBack: () => ksaBackOrHome(context),
      body: ref.watch(myRequestsProvider).when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error: (_, __) => KsaErrorState(
              message: l10n.requestsError,
              retryLabel: l10n.retryLabel,
              onRetry: () => ref.invalidate(myRequestsProvider),
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

    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        _NewRequestButton(label: l10n.requestNew, onTap: () => unawaited(_openNewRequest())),
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
          KsaEmptyState(
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
    );
  }
}

/// The gold "طلب جديد" call-to-action.
class _NewRequestButton extends StatelessWidget {
  const _NewRequestButton({required this.label, required this.onTap});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Container(
        height: 46,
        decoration: BoxDecoration(
          color: SimfTokens.accent,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            const Icon(Icons.add, size: 18, color: SimfTokens.navy),
            const SizedBox(width: SimfTokens.space2),
            Text(
              label,
              style: const TextStyle(
                color: SimfTokens.navy,
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The horizontally-scrolling status filter chips (All + each populated status,
/// with counts), matching the الطلبات chip row.
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
    final chips = <Widget>[
      _chip(label: l10n.requestStatusAll, count: items.length, status: null),
    ];
    for (final status in AppRequestStatus.values) {
      final count = items.where((i) => i.status == status).length;
      if (count == 0) {
        continue;
      }
      chips.add(const SizedBox(width: SimfTokens.space2));
      chips.add(_chip(label: _statusLabel(l10n, status), count: count, status: status));
    }
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(children: chips),
    );
  }

  Widget _chip({
    required String label,
    required int count,
    required AppRequestStatus? status,
  }) {
    final active = selected == status;
    return InkWell(
      onTap: () => onSelect(status),
      borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space2,
        ),
        decoration: BoxDecoration(
          color: active ? SimfTokens.accent : SimfTokens.navyDeep,
          borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
          border: Border.all(
            color: active ? SimfTokens.accent : SimfTokens.line,
            width: SimfTokens.hairline,
          ),
        ),
        child: Text(
          '$label ($count)',
          style: TextStyle(
            color: active ? SimfTokens.navy : SimfTokens.beigeBorder,
            fontSize: SimfTokens.textSm,
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
      return SimfTokens.success;
    case AppRequestStatus.rejected:
      return SimfTokens.danger;
    case AppRequestStatus.cancelled:
      return SimfTokens.beigeBorder;
    case AppRequestStatus.pending:
      return SimfTokens.accent;
  }
}

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
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border(left: BorderSide(color: statusColor, width: 3)),
      ),
      child: Column(
        children: <Widget>[
          InkWell(
            onTap: () => setState(() => _expanded = !_expanded),
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            child: Padding(
              padding: const EdgeInsets.all(SimfTokens.space3),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  _IconBox(icon: _kindIcon(item.kind)),
                  const SizedBox(width: SimfTokens.space3),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          _kindHeadline(l10n, item.kind),
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: SimfTokens.textMd,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        if (subtitle.isNotEmpty) ...<Widget>[
                          const SizedBox(height: SimfTokens.space1),
                          Text(
                            subtitle,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: SimfTokens.beigeBorder,
                              fontSize: SimfTokens.textSm,
                              height: 1.4,
                            ),
                          ),
                        ],
                        const SizedBox(height: SimfTokens.space1),
                        Text(
                          l10n.requestDate(item.displayDate.toLocal()),
                          textDirection: TextDirection.ltr,
                          style: const TextStyle(
                            color: SimfTokens.timestampMuted,
                            fontSize: SimfTokens.textXs,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: SimfTokens.space2),
                  Icon(
                    _expanded ? Icons.expand_less : Icons.expand_more,
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

/// The gold rounded type-icon box at the inline start of a card.
class _IconBox extends StatelessWidget {
  const _IconBox({required this.icon});

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 36,
      height: 36,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Icon(icon, size: 18, color: SimfTokens.navy),
    );
  }
}
