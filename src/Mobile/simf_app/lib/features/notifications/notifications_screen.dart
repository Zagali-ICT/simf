import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart' hide TextDirection;
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_search_field.dart';
import '../../app/widgets/ksa_shell.dart';
import 'data/notification_models.dart';
import 'data/notifications_repository.dart';

/// The three filter chips → notification `kind` names (D-053 enum).
const Set<String> _sessionKinds = <String>{
  'BookingConfirmed',
  'SessionReminder',
  'BookingRejected',
  'MeetingScheduled',
  'MeetingCancelled',
  'SessionRatingRequest',
};
const Set<String> _vipKinds = <String>{'InvitationReceived', 'VipBroadcast'};

enum _NotifFilter { all, sessions, vip }

/// One 12-hour formatter for the card timestamps (hoisted off the build path).
final DateFormat _timeFormat = DateFormat('hh:mm a');
final DateFormat _dateFormat = DateFormat('d MMM');

/// Page 033 — الإشعارات · Notifications (#33, `/notifications`), rebuilt to the
/// KSA frame **223:4264** on the shared shell.
///
/// **Approved-account only** (`RequireApprovedAccount`). One read returns the
/// first page (`POST …/notifications/list`); the screen renders a search box,
/// the **الكل / جلسات / VIP** filter chips (mapped to the notification kinds),
/// the list grouped by **اليوم / أمس / date**, and a per-severity category icon
/// with an unread dot. Tapping an unread row marks it read then refreshes; the
/// trailing "mark all read" action clears every unread.
class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({super.key});

  @override
  ConsumerState<NotificationsScreen> createState() =>
      _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  bool _loading = true;
  bool _error = false;
  bool _markingAll = false;
  List<NotificationItem> _items = const <NotificationItem>[];
  String _query = '';
  _NotifFilter _filter = _NotifFilter.all;

  @override
  void initState() {
    super.initState();
    unawaited(_openInbox());
  }

  /// #13 — opening the inbox loads the list, then marks everything read so an
  /// opened inbox never stays unread and the Home bell badge clears. (The
  /// backend models read/unread only — there is no separate "seen" state.)
  Future<void> _openInbox() async {
    await _load();
    if (!mounted || _error) {
      return;
    }
    if (_items.any((n) => !n.isRead)) {
      await _markAllRead(reload: false);
    }
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final items =
          await ref.read(notificationsRepositoryProvider).getNotifications();
      if (!mounted) {
        return;
      }
      setState(() {
        _items = items;
        _loading = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _loading = false;
        _error = true;
      });
    }
  }

  Future<void> _onTapItem(NotificationItem item) async {
    // Deep-link first so an actionable notification always navigates, even if
    // the best-effort mark-read below leaves this screen unmounted (the prior
    // `if (!mounted) return` after the await skipped the deep-link).
    _maybeDeepLink(item);
    // Mark unread items read (best effort).
    if (!item.isRead) {
      try {
        await ref.read(notificationsRepositoryProvider).markRead(item.id);
      } on ApiFailure {
        // Best effort — leave the item unread on failure.
      }
      if (!mounted) {
        return;
      }
      // #14 — clear the Home bell badge (a separate count provider) + flip the
      // item locally instead of a full reload.
      ref.invalidate(unreadNotificationCountProvider);
      setState(() {
        _items = _items
            .map((n) => n.id == item.id ? n.markedRead() : n)
            .toList(growable: false);
      });
    }
  }

  /// Deep-links from an actionable notification. The end-of-session prompt
  /// (`SessionRatingRequest`) carries the session id in `relatedEntityId`; tap
  /// opens the Session rating form for it.
  void _maybeDeepLink(NotificationItem item) {
    if (item.kind == 'SessionRatingRequest' &&
        (item.relatedEntityId ?? '').isNotEmpty) {
      context.pushNamed(
        RouteNames.rate,
        queryParameters: <String, String>{
          'code': 'Session',
          'targetId': item.relatedEntityId!,
        },
      );
    }
  }

  Future<void> _onMarkAll(AppL10n l10n) async {
    setState(() => _markingAll = true);
    final messenger = ScaffoldMessenger.of(context);
    final ok = await _markAllRead(reload: false);
    if (!mounted) {
      return;
    }
    setState(() => _markingAll = false);
    if (!ok) {
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.notificationsMarkAllFailed)),
      );
    }
  }

  /// Marks every notification read on the server, clears the Home bell badge
  /// (#14 — a separate count provider that the screen must invalidate), and
  /// reflects the read state locally (or re-fetches when [reload]).
  Future<bool> _markAllRead({required bool reload}) async {
    try {
      await ref.read(notificationsRepositoryProvider).markAllRead();
    } on ApiFailure {
      return false;
    }
    ref.invalidate(unreadNotificationCountProvider);
    if (!mounted) {
      return true;
    }
    if (reload) {
      await _load();
    } else {
      setState(() {
        _items = _items.map((n) => n.markedRead()).toList(growable: false);
      });
    }
    return true;
  }

  /// Items after the active chip + search filter, newest-first order preserved.
  List<NotificationItem> _visibleItems(bool isArabic) {
    Iterable<NotificationItem> it = _items;
    switch (_filter) {
      case _NotifFilter.sessions:
        it = it.where((n) => _sessionKinds.contains(n.kind));
      case _NotifFilter.vip:
        it = it.where((n) => _vipKinds.contains(n.kind));
      case _NotifFilter.all:
        break;
    }
    final q = _query.trim().toLowerCase();
    if (q.isNotEmpty) {
      it = it.where(
        (n) =>
            n.localizedTitle(isArabic).toLowerCase().contains(q) ||
            n.localizedBody(isArabic).toLowerCase().contains(q),
      );
    }
    return it.toList(growable: false);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return KsaPage(
      title: l10n.notificationsTitle,
      onBack: () => ksaBackOrHome(context),
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return KsaRefresh(
        onRefresh: _load,
        child: _PullableState(
          child: KsaErrorState(
            message: l10n.notificationsError,
            retryLabel: l10n.retryLabel,
            onRetry: () => unawaited(_load()),
          ),
        ),
      );
    }
    if (_items.isEmpty) {
      return KsaRefresh(
        onRefresh: _load,
        child: _PullableState(
          child: KsaEmptyState(
            icon: Icons.notifications_none_outlined,
            message: l10n.notificationsEmpty,
          ),
        ),
      );
    }
    final isArabic = l10n.isArabic;
    final hasUnread = _items.any((n) => !n.isRead);
    final visible = _visibleItems(isArabic);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Padding(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            SimfTokens.space2,
            SimfTokens.space4,
            SimfTokens.space2,
          ),
          child: KsaSearchField(
            hint: l10n.notificationsSearchHint,
            onChanged: (v) => setState(() => _query = v),
            showTuningIcon: true,
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
          child: Row(
            children: <Widget>[
              _Chip(
                label: l10n.notificationsFilterAll,
                selected: _filter == _NotifFilter.all,
                onTap: () => setState(() => _filter = _NotifFilter.all),
              ),
              const SizedBox(width: SimfTokens.space2),
              _Chip(
                label: l10n.notificationsFilterSessions,
                selected: _filter == _NotifFilter.sessions,
                onTap: () => setState(() => _filter = _NotifFilter.sessions),
              ),
              const SizedBox(width: SimfTokens.space2),
              _Chip(
                label: l10n.notificationsFilterVip,
                selected: _filter == _NotifFilter.vip,
                onTap: () => setState(() => _filter = _NotifFilter.vip),
              ),
              const Spacer(),
              if (hasUnread)
                TextButton(
                  onPressed:
                      _markingAll ? null : () => unawaited(_onMarkAll(l10n)),
                  child: Text(
                    l10n.notificationsMarkAll,
                    style: const TextStyle(
                      color: SimfTokens.accent,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        Expanded(
          child: KsaRefresh(
            onRefresh: _load,
            child: visible.isEmpty
                ? _PullableState(
                    child: KsaEmptyState(
                      icon: Icons.search_off_outlined,
                      message: l10n.notificationsNoMatches,
                    ),
                  )
                : _GroupedList(
                    items: visible,
                    isArabic: isArabic,
                    l10n: l10n,
                    onTap: (item) => unawaited(_onTapItem(item)),
                  ),
          ),
        ),
      ],
    );
  }
}

/// Hosts a non-scrollable state widget (empty / no-matches / error) inside an
/// always-scrollable, viewport-tall box so the surrounding [KsaRefresh] can fire
/// its pull-to-retry gesture even when the state itself does not scroll.
class _PullableState extends StatelessWidget {
  const _PullableState({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        return SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          child: ConstrainedBox(
            constraints: BoxConstraints(minHeight: constraints.maxHeight),
            child: child,
          ),
        );
      },
    );
  }
}

/// One filter chip: gold-filled when selected, gold-outlined otherwise.
class _Chip extends StatelessWidget {
  const _Chip({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Frame 758:2491 — gold fill when selected, beige 0.2 hairline otherwise;
    // radius 4, 14px SemiBold, label always white.
    return Material(
      color: selected ? SimfTokens.accent : Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Text(
            label,
            style: const TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.w600,
              fontSize: SimfTokens.textMd,
            ),
          ),
        ),
      ),
    );
  }
}

/// The day-grouped list: a "اليوم / أمس / date" header per run of same-day
/// items, then the cards for that day.
class _GroupedList extends StatelessWidget {
  const _GroupedList({
    required this.items,
    required this.isArabic,
    required this.l10n,
    required this.onTap,
  });

  final List<NotificationItem> items;
  final bool isArabic;
  final AppL10n l10n;
  final ValueChanged<NotificationItem> onTap;

  @override
  Widget build(BuildContext context) {
    // Build an ordered (dayLabel, items) list, preserving the newest-first
    // order the API returns.
    final groups = <MapEntry<String, List<NotificationItem>>>[];
    for (final item in items) {
      final label = _dayLabel(item.createdAt);
      if (groups.isNotEmpty && groups.last.key == label) {
        groups.last.value.add(item);
      } else {
        groups.add(MapEntry(label, <NotificationItem>[item]));
      }
    }
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        0,
        SimfTokens.space4,
        SimfTokens.space4,
      ),
      children: <Widget>[
        for (final group in groups) ...<Widget>[
          if (group.key.isNotEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: SimfTokens.space2),
              child: Text(
                group.key,
                // Frame 758:2491 — 16px Medium, white.
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: SimfTokens.textLg,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ),
          for (final item in group.value)
            _NotificationCard(
              item: item,
              isArabic: isArabic,
              dayLabel: group.key,
              onTap: () => onTap(item),
            ),
        ],
      ],
    );
  }

  String _dayLabel(DateTime? createdAt) {
    if (createdAt == null) {
      return '';
    }
    final local = createdAt.toLocal();
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final d = DateTime(local.year, local.month, local.day);
    final diff = today.difference(d).inDays;
    if (diff == 0) {
      return l10n.dayToday;
    }
    if (diff == 1) {
      return l10n.dayYesterday;
    }
    return _dateFormat.format(local);
  }
}

/// One notification card (frame node): a solid severity-coloured circular icon
/// at the inline end, the bold title + body + "{day} · {time}" line, and the
/// gold unread dot at the inline start.
class _NotificationCard extends StatelessWidget {
  const _NotificationCard({
    required this.item,
    required this.isArabic,
    required this.dayLabel,
    required this.onTap,
  });

  final NotificationItem item;
  final bool isArabic;
  final String dayLabel;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final unread = !item.isRead;
    final time = item.createdAt == null
        ? null
        : _timeFormat.format(item.createdAt!.toLocal());
    // Frame 758:2491 — "{time} · {day}" order.
    final stamp = time == null
        ? null
        : (dayLabel.isEmpty ? time : '$time · $dayLabel');
    return Padding(
      padding: const EdgeInsets.only(bottom: SimfTokens.space2),
      // Frame 758:2491 — every card is the navyDeep fill, borderless; the
      // category mark sits at the inline start and an unread card carries a
      // red dot at the top inline-end corner.
      child: KsaCard(
        // Actionable notifications must stay tappable after the inbox
        // auto-marks them read (_openInbox), otherwise the SessionRatingRequest
        // deep-link is unreachable. _onTapItem is a no-op for read,
        // non-actionable rows.
        onTap: onTap,
        color: SimfTokens.navyDeep,
        borderColor: Colors.transparent,
        borderWidth: 0,
        child: Stack(
          children: <Widget>[
            Padding(
              padding: const EdgeInsets.all(SimfTokens.space2),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  _CategoryIcon(severity: item.severity),
                  const SizedBox(width: SimfTokens.space3),
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
                        const SizedBox(height: 6),
                        Text(
                          item.localizedBody(isArabic),
                          style: const TextStyle(
                            color: SimfTokens.beigeBorder,
                            fontSize: SimfTokens.textSm,
                            height: 1.5,
                          ),
                        ),
                        if (stamp != null) ...<Widget>[
                          const SizedBox(height: 6),
                          Text(
                            stamp,
                            style: const TextStyle(
                              color: SimfTokens.timestampMuted,
                              fontSize: SimfTokens.textSm,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ],
              ),
            ),
            if (unread)
              const PositionedDirectional(
                top: SimfTokens.space2,
                end: SimfTokens.space2,
                child: _UnreadDot(),
              ),
          ],
        ),
      ),
    );
  }
}

/// The unread marker (frame 758:2491) — a 14-px red dot at the card's top
/// inline-end corner.
class _UnreadDot extends StatelessWidget {
  const _UnreadDot();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 14,
      height: 14,
      decoration: const BoxDecoration(
        color: SimfTokens.danger,
        shape: BoxShape.circle,
      ),
    );
  }
}

/// The solid colour-coded circular category mark — severity drives the colour
/// (success=green, error=red, warning/info=gold) and the glyph.
class _CategoryIcon extends StatelessWidget {
  const _CategoryIcon({required this.severity});

  final NotificationSeverity severity;

  @override
  Widget build(BuildContext context) {
    final (color, icon) = switch (severity) {
      NotificationSeverity.success => (
          SimfTokens.success,
          Icons.check_rounded,
        ),
      NotificationSeverity.error => (SimfTokens.danger, Icons.close_rounded),
      NotificationSeverity.warning => (
          SimfTokens.accent,
          Icons.priority_high_rounded,
        ),
      NotificationSeverity.info => (
          SimfTokens.accent,
          Icons.mail_outline_rounded,
        ),
    };
    return Container(
      width: 40,
      height: 40,
      decoration: BoxDecoration(color: color, shape: BoxShape.circle),
      child: Icon(icon, size: 20, color: Colors.white),
    );
  }
}
