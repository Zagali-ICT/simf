import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'data/notification_models.dart';
import 'data/notifications_repository.dart';

/// Page 033 — الإشعارات · Notifications (#33, `/notifications`, signed-in only).
///
/// **Approved-account only** (`RequireApprovedAccount`). One read returns the
/// first page of notifications (`POST …/notifications/list`); the screen renders
/// a severity-coded card per item with an unread dot. Tapping an unread row
/// marks it read (`POST …/{id}/read`) then refreshes; the app-bar action marks
/// every notification read (`POST …/read-all`). Loading / empty / error+retry
/// states. UI is interim until the SIMF-VID-001 pass.
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

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final items = await ref.read(notificationsRepositoryProvider).getNotifications();
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
    if (item.isRead) {
      return;
    }
    try {
      await ref.read(notificationsRepositoryProvider).markRead(item.id);
    } on ApiFailure {
      // Best effort — a failed read leaves the unread dot; the refresh below
      // re-syncs with the server's truth.
    }
    if (!mounted) {
      return;
    }
    await _load();
  }

  Future<void> _onMarkAll(AppL10n l10n) async {
    setState(() => _markingAll = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(notificationsRepositoryProvider).markAllRead();
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() => _markingAll = false);
      messenger.showSnackBar(SnackBar(content: Text(l10n.notificationsMarkAllFailed)));
      return;
    }
    if (!mounted) {
      return;
    }
    setState(() => _markingAll = false);
    await _load();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final hasUnread = _items.any((n) => !n.isRead);
    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.notificationsTitle),
        actions: <Widget>[
          if (!_loading && !_error && hasUnread)
            IconButton(
              tooltip: l10n.notificationsMarkAll,
              onPressed: _markingAll ? null : () => unawaited(_onMarkAll(l10n)),
              icon: const Icon(Icons.done_all),
            ),
        ],
      ),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return _Error(
        message: l10n.notificationsError,
        onRetry: () => unawaited(_load()),
      );
    }
    if (_items.isEmpty) {
      return _Empty(message: l10n.notificationsEmpty);
    }
    final isArabic = l10n.isArabic;
    return ListView.builder(
      padding: const EdgeInsets.all(SimfTokens.space4),
      itemCount: _items.length,
      itemBuilder: (context, index) {
        final item = _items[index];
        return _NotificationCard(
          item: item,
          isArabic: isArabic,
          onTap: () => unawaited(_onTapItem(item)),
        );
      },
    );
  }
}

class _NotificationCard extends StatelessWidget {
  const _NotificationCard({
    required this.item,
    required this.isArabic,
    required this.onTap,
  });

  final NotificationItem item;
  final bool isArabic;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: SimfTokens.space2),
      clipBehavior: Clip.antiAlias,
      child: ListTile(
        onTap: item.isRead ? null : onTap,
        leading: Icon(
          _severityIcon(item.severity),
          color: _severityColor(item.severity),
        ),
        title: Text(
          item.localizedTitle(isArabic),
          style: TextStyle(
            fontWeight: item.isRead ? FontWeight.w600 : FontWeight.w700,
          ),
        ),
        subtitle: Text(item.localizedBody(isArabic)),
        trailing: item.isRead
            ? null
            : Container(
                width: SimfTokens.space2,
                height: SimfTokens.space2,
                decoration: const BoxDecoration(
                  color: SimfTokens.accent,
                  shape: BoxShape.circle,
                ),
              ),
      ),
    );
  }
}

IconData _severityIcon(NotificationSeverity severity) {
  switch (severity) {
    case NotificationSeverity.success:
      return Icons.check_circle_outline;
    case NotificationSeverity.warning:
      return Icons.warning_amber_outlined;
    case NotificationSeverity.error:
      return Icons.error_outline;
    case NotificationSeverity.info:
      return Icons.info_outline;
  }
}

Color _severityColor(NotificationSeverity severity) {
  switch (severity) {
    case NotificationSeverity.success:
      return SimfTokens.success;
    case NotificationSeverity.warning:
      return SimfTokens.accent;
    case NotificationSeverity.error:
      return SimfTokens.danger;
    case NotificationSeverity.info:
      return SimfTokens.navy;
  }
}

class _Empty extends StatelessWidget {
  const _Empty({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(
            Icons.notifications_none_outlined,
            size: 56,
            color: SimfTokens.inkMuted,
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.inkMuted)),
        ],
      ),
    );
  }
}

class _Error extends StatelessWidget {
  const _Error({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
