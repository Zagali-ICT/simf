import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_search_field.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/notifications/data/notification_models.dart';
import 'package:simf_app/features/notifications/data/notifications_repository.dart';
import 'package:simf_app/features/notifications/notification_filters.dart';
import 'package:simf_app/features/notifications/widgets/notification_grouped_list.dart';
import 'package:simf_app/features/notifications/widgets/notifications_filter_bar.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

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

/// Notifications — route: `RouteNames.notifications` · Figma 223:4264
class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({super.key});

  @override
  ConsumerState<NotificationsScreen> createState() =>
      _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  bool _markingAll = false;
  String _query = '';
  NotificationFilter _filter = NotificationFilter.all;

  /// Ids this screen has SUCCESSFULLY told the server are read.
  ///
  /// The rows flip without a reload, which is deliberate (#14) and is the one
  /// thing a provider cannot do by mutation. Applied as an overlay at render
  /// and cleared whenever fresh data arrives, because the server is then
  /// authoritative — the old code got the same effect by replacing the whole
  /// `_items` list on reload.
  final Set<String> _readLocally = <String>{};

  @override
  void initState() {
    super.initState();
    unawaited(_openInbox());
  }

  /// #13 — opening the inbox loads the list, then marks everything read so an
  /// opened inbox never stays unread and the Home bell badge clears. (The
  /// backend models read/unread only — there is no separate "seen" state.)
  ///
  /// Awaits the provider's first future rather than listening, so it still runs
  /// exactly ONCE per inbox open. Hooking it to every data arrival would also
  /// fire it on each pull-to-refresh.
  Future<void> _openInbox() async {
    final List<NotificationItem> items;
    try {
      items = await ref.read(notificationsListProvider.future);
    } on Object {
      return; // The error branch renders; nothing to mark.
    }
    if (!mounted || !items.any((n) => !n.isRead)) {
      return;
    }
    await _markAllRead(items);
  }

  Future<void> _refresh() async {
    _readLocally.clear();
    await refreshAsync(ref, notificationsListProvider.future);
  }

  /// The server list with this screen's own read-flips applied.
  List<NotificationItem> _withLocalReads(List<NotificationItem> items) =>
      _readLocally.isEmpty
          ? items
          : items
              .map((n) => _readLocally.contains(n.id) ? n.markedRead() : n)
              .toList(growable: false);

  Future<void> _onTapItem(NotificationItem item) async {
    // Deep-link first so an actionable notification always navigates, even if
    // the best-effort mark-read below leaves this screen unmounted (the prior
    // `if (!mounted) return` after the await skipped the deep-link).
    _maybeDeepLink(item);
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
      setState(() => _readLocally.add(item.id));
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
    final ok = await _markAllRead(_currentItems);
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
  /// reflects the read state locally rather than re-fetching.
  ///
  /// The `reload: true` branch this replaced had no caller — both sites passed
  /// false — so the parameter went with it.
  Future<bool> _markAllRead(List<NotificationItem> items) async {
    try {
      await ref.read(notificationsRepositoryProvider).markAllRead();
    } on ApiFailure {
      return false;
    }
    ref.invalidate(unreadNotificationCountProvider);
    if (!mounted) {
      return true;
    }
    setState(() => _readLocally.addAll(items.map((n) => n.id)));
    return true;
  }

  /// Whatever the provider currently holds, for the mark-all button. Empty
  /// while loading or on failure, where the button is not reachable anyway.
  List<NotificationItem> get _currentItems =>
      ref.read(notificationsListProvider).valueOrNull ??
      const <NotificationItem>[];

  /// Items after the active chip + search filter, newest-first order preserved.
  List<NotificationItem> _visibleItems(
    List<NotificationItem> items,
    bool isArabic,
  ) {
    Iterable<NotificationItem> it = items;
    switch (_filter) {
      case NotificationFilter.sessions:
        it = it.where((n) => sessionsChipGroups.contains(groupForItem(n)));
      case NotificationFilter.vip:
        it = it.where((n) => vipChipGroups.contains(groupForItem(n)));
      case NotificationFilter.all:
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
    return ref.watch(notificationsListProvider).when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => SimfRefreshableMessage(
            onRefresh: _refresh,
            child: SimfErrorState(
              message: l10n.notificationsError,
              retryLabel: l10n.retryLabel,
              onRetry: () => ref.invalidate(notificationsListProvider),
            ),
          ),
          data: (serverItems) {
            final items = _withLocalReads(serverItems);
            return items.isEmpty
                ? SimfRefreshableMessage(
                    onRefresh: _refresh,
                    child: SimfEmptyState(
                      icon: Icons.notifications_none_outlined,
                      message: l10n.notificationsEmpty,
                    ),
                  )
                : _buildInbox(l10n, items);
          },
        );
  }

  Widget _buildInbox(AppL10n l10n, List<NotificationItem> items) {
    final isArabic = l10n.isArabic;
    final hasUnread = items.any((n) => !n.isRead);
    final visible = _visibleItems(items, isArabic);
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
          child: NotificationsFilterBar(
            l10n: l10n,
            filter: _filter,
            onFilter: (filter) => setState(() => _filter = filter),
            showMarkAll: hasUnread,
            onMarkAll: _markingAll ? null : () => unawaited(_onMarkAll(l10n)),
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        Expanded(
          child: SimfPullToRefresh(
            onRefresh: _refresh,
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
