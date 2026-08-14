import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_search_field.dart';
import 'package:simf_app/features/notifications/data/notification_models.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';
import 'package:simf_app/features/notifications/widgets/notification_filter_chip.dart';
import 'package:simf_app/features/notifications/widgets/notification_grouped_list.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The chips filter by the server `group` code (D-678). The "جلسات" chip covers
/// the event-flow groups; "VIP" covers the VIP group; "الكل" shows everything.
const Set<String> _sessionsChipGroups = <String>{
  'Sessions',
  'Bookings',
  'Meetings',
  'Ratings',
};
const Set<String> _vipChipGroups = <String>{'Vip'};

/// The only in-app locations a notification `clickUrl` may open — a guard so a
/// stale or foreign value never pushes an unknown route (the router has no
/// error page). Only the path is matched; the query string is ignored (D-678).
const Set<String> _allowedClickPaths = <String>{
  '/rate',
  '/badge',
  // Bi-Meeting rework — the other-party confirm deep-link (?requestId=…).
  '/meeting-confirm',
  // QA A27 — the meeting-lifecycle tiles (scheduled / cancelled / confirmed /
  // 15-minute reminder) open the bilateral-meetings page. Without this entry
  // the server clickUrl was rejected here and every such tile stayed inert.
  '/meetings',
};

/// The group for [item]: the server `group`, or a client fallback derived from
/// the kind for rows created before the group column existed.
String _groupForItem(NotificationItem item) {
  final group = item.group?.trim();
  if (group != null && group.isNotEmpty) {
    return group;
  }
  switch (item.kind) {
    case 'BookingConfirmed':
    case 'BookingRejected':
      return 'Bookings';
    case 'SessionReminder':
      return 'Sessions';
    case 'MeetingScheduled':
    case 'MeetingCancelled':
    // Bi-Meeting rework — the other-party confirm request + the 15-min
    // reminder.
    case 'MeetingRequested':
    case 'MeetingReminder':
      return 'Meetings';
    case 'InvitationReceived':
    case 'VipBroadcast':
      return 'Vip';
    case 'SessionRatingRequest':
    case 'DayRatingRequest':
    case 'EventRatingRequest':
    case 'AppRatingRequest':
    case 'ExhibitionRatingRequest':
      return 'Ratings';
    default:
      return 'Account';
  }
}

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

  /// Deep-links from an actionable notification. Prefers the server `clickUrl`
  /// (an app-internal location like `/rate?code=Session&targetId=…` or
  /// `/badge`), restricted to a known-route allowlist so a stale/foreign value
  /// never lands on the router's (error-page-less) fallback. Falls back to the
  /// kind-based routes for rows created before the clickUrl column (D-678,
  /// generalises the D-672 hardcode).
  void _maybeDeepLink(NotificationItem item) {
    final clickUrl = item.clickUrl?.trim();
    if (clickUrl != null && clickUrl.isNotEmpty) {
      final uri = Uri.tryParse(clickUrl);
      if (uri != null && _allowedClickPaths.contains(uri.path)) {
        unawaited(context.push(clickUrl));
        return;
      }
    }
    // Fallback for pre-migration rows (no/again-null clickUrl).
    if (item.kind == 'SessionRatingRequest' &&
        (item.relatedEntityId ?? '').isNotEmpty) {
      unawaited(context.pushNamed(
          RouteNames.rate,
          queryParameters: <String, String>{
            'code': 'Session',
            'targetId': item.relatedEntityId!,
          },
        ),);
      return;
    }
    // "بطاقتك الذكية جاهزة" (AccountApproved) and BookingConfirmed both land on
    // the badge/QR screen (758-1469) so a tap opens the user's entry QR even
    // when the row predates the clickUrl column.
    if (item.kind == 'BookingConfirmed' || item.kind == 'AccountApproved') {
      unawaited(context.pushNamed(RouteNames.badge));
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
        it = it.where((n) => _sessionsChipGroups.contains(_groupForItem(n)));
      case _NotifFilter.vip:
        it = it.where((n) => _vipChipGroups.contains(_groupForItem(n)));
      case _NotifFilter.all:
        break;
    }
    final q = _query.trim().toLowerCase();
    if (q.isNotEmpty) {
      it = it.where(
        (n) =>
            n.localizedTitle(isArabic: isArabic).toLowerCase().contains(q) ||
            n.localizedBody(isArabic: isArabic).toLowerCase().contains(q),
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
      return SimfRefreshableMessage(
        onRefresh: _load,
        child: SimfErrorState(
          message: l10n.notificationsError,
          retryLabel: l10n.retryLabel,
          onRetry: () => unawaited(_load()),
        ),
      );
    }
    if (_items.isEmpty) {
      return SimfRefreshableMessage(
        onRefresh: _load,
        child: SimfEmptyState(
          icon: Icons.notifications_none_outlined,
          message: l10n.notificationsEmpty,
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
                    style: SimfTokens.labelGoldSm,
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
