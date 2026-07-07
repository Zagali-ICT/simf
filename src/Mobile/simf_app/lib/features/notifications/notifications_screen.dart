import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_search_field.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/notification_models.dart';
import 'data/notifications_repository.dart';
import 'widgets/notification_filter_chip.dart';
import 'widgets/notification_grouped_list.dart';

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
  /// opens the Session rating form for it. A `BookingConfirmed` notification
  /// means the visitor's entry badge is now live; tap opens the personal QR
  /// badge they scan at the gate.
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
      return;
    }
    if (item.kind == 'BookingConfirmed') {
      context.pushNamed(RouteNames.badge);
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
    return SimfPageShell(
      title: l10n.notificationsTitle,
      onBack: () => backOrHome(context),
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return SimfPullToRefresh(
        onRefresh: _load,
        child: SimfPullableHost(
          child: SimfErrorState(
            message: l10n.notificationsError,
            retryLabel: l10n.retryLabel,
            onRetry: () => unawaited(_load()),
          ),
        ),
      );
    }
    if (_items.isEmpty) {
      return SimfPullToRefresh(
        onRefresh: _load,
        child: SimfPullableHost(
          child: SimfEmptyState(
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
          child: SimfSearchField(
            hint: l10n.notificationsSearchHint,
            onChanged: (v) => setState(() => _query = v),
            showTuningIcon: true,
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
          child: Row(
            children: <Widget>[
              NotificationFilterChip(
                label: l10n.notificationsFilterAll,
                selected: _filter == _NotifFilter.all,
                onTap: () => setState(() => _filter = _NotifFilter.all),
              ),
              const SizedBox(width: SimfTokens.space2),
              NotificationFilterChip(
                label: l10n.notificationsFilterSessions,
                selected: _filter == _NotifFilter.sessions,
                onTap: () => setState(() => _filter = _NotifFilter.sessions),
              ),
              const SizedBox(width: SimfTokens.space2),
              NotificationFilterChip(
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
          child: SimfPullToRefresh(
            onRefresh: _load,
            child: visible.isEmpty
                ? SimfPullableHost(
                    child: SimfEmptyState(
                      icon: Icons.search_off_outlined,
                      message: l10n.notificationsNoMatches,
                    ),
                  )
                : NotificationGroupedList(
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
