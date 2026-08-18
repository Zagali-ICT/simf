import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';
import 'package:simf_app/features/moderation/data/moderation_repository.dart';
import 'package:simf_app/features/moderation/widgets/moderator_desk.dart';
import 'package:simf_app/features/moderation/widgets/moderator_header.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Moderator Q&A desk — route: RouteNames.sessionModerate · Figma 1461:12227
/// Contract: authority is the per-session `SessionModerator` grant (or
/// Administrator), **not** the mobile `AppRole.moderator` (D-405 / D-509); a
/// moderator without it gets a 403. DEF-MOD-001 / DEF-MOD-002 — every chip and
/// action is backed by the persisted `QuestionStatus`, so nothing is lost when
/// the desk is closed and a mis-clicked reject stays recoverable.
class SessionModerateScreen extends ConsumerStatefulWidget {
  const SessionModerateScreen({required this.sessionId, super.key});

  final String sessionId;

  @override
  ConsumerState<SessionModerateScreen> createState() =>
      _SessionModerateScreenState();
}

class _SessionModerateScreenState extends ConsumerState<SessionModerateScreen> {
  ModeratorQueueFilter _filter = ModeratorQueueFilter.all;

  /// The current buckets, or empty while loading / on failure — where no
  /// action is reachable anyway.
  ModeratorQueues get _queues =>
      ref.read(moderatorQueuesProvider(widget.sessionId)).value ??
      const ModeratorQueues.empty();

  List<ModeratorQuestion> get _desk => _queues.desk;
  List<ModeratorQuestion> get _rejected => _queues.rejected;

  /// The optimistic swap the actions used to make with `setState`.
  void _apply({
    List<ModeratorQuestion>? desk,
    List<ModeratorQuestion>? rejected,
  }) =>
      ref.read(moderatorQueuesProvider(widget.sessionId).notifier).apply(
            ModeratorQueues(
              desk: desk ?? _desk,
              rejected: rejected ?? _rejected,
            ),
          );

  Future<void> _refresh() =>
      refreshAsync(ref, moderatorQueuesProvider(widget.sessionId).future);

  /// يتم الإجابة — push the question on stage. The server only pushes an
  /// APPROVED question, so a rejected / answered row is returned to Approved
  /// first (the same two-step the desk already used for a rejected row).
  Future<void> _push(ModeratorQuestion q) async {
    if (q.isRejected) {
      if (!await _restore(q)) {
        return;
      }
    } else if (q.isAnswered) {
      if (!await _setAnswered(q, false)) {
        return;
      }
    }
    await _act(
      () => ref.read(moderationRepositoryProvider).push(widget.sessionId, q.id),
    );
  }

  /// مرفوض — reject (real `hide`). Optimistically moves the row to the مرفوض
  /// tab; the move is undone if the call fails.
  Future<void> _reject(ModeratorQuestion q) async {
    final deskBefore = _desk;
    final rejectedBefore = _rejected;
    _apply(
      desk: _without(_desk, q.id),
      rejected: <ModeratorQuestion>[
        ..._without(_rejected, q.id),
        q.withStatus(ModeratorQuestionStatus.hidden),
      ],
    );
    final ok = await _act(
      () => ref.read(moderationRepositoryProvider).setHidden(
            widget.sessionId,
            q.id,
            isHidden: true,
          ),
    );
    if (!ok && mounted) {
      _apply(desk: deskBefore, rejected: rejectedBefore);
    }
  }

  /// Restore a previously rejected question (un-hide) back into the desk.
  /// DEF-MOD-002 — the rejected rows come from the server, so a mis-click stays
  /// recoverable after leaving the screen.
  Future<bool> _restore(ModeratorQuestion q) async {
    final deskBefore = _desk;
    final rejectedBefore = _rejected;
    _apply(
      rejected: _without(_rejected, q.id),
      desk: <ModeratorQuestion>[
        ..._without(_desk, q.id),
        q.withStatus(ModeratorQuestionStatus.approved),
      ],
    );
    final ok = await _act(
      () => ref.read(moderationRepositoryProvider).setHidden(
            widget.sessionId,
            q.id,
            isHidden: false,
          ),
    );
    if (!ok && mounted) {
      _apply(desk: deskBefore, rejected: rejectedBefore);
    }
    return ok;
  }

  /// تمت الإجابة — DEF-MOD-001: persist the answered mark (toggles). A rejected
  /// row is restored first so it lands back on the desk as answered.
  Future<void> _toggleAnswered(ModeratorQuestion q) async {
    if (q.isRejected) {
      if (!await _restore(q)) {
        return;
      }
      await _setAnswered(q, true);
      return;
    }
    await _setAnswered(q, !q.isAnswered);
  }

  /// Writes the answered mark with an optimistic row swap + rollback.
  Future<bool> _setAnswered(ModeratorQuestion q, bool isAnswered) async {
    final deskBefore = _desk;
    _apply(
      desk: _replace(
        _desk,
        q.withStatus(
          isAnswered
              ? ModeratorQuestionStatus.answered
              : ModeratorQuestionStatus.approved,
        ),
      ),
    );
    final ok = await _act(
      () => ref.read(moderationRepositoryProvider).setAnswered(
            widget.sessionId,
            q.id,
            isAnswered: isAnswered,
          ),
    );
    if (!ok && mounted) {
      _apply(desk: deskBefore);
    }
    return ok;
  }

  /// FR-MOD-003 — drag-to-reorder. `PUT …/questions/reorder` replaces the whole
  /// desk order and REQUIRES every working-desk question exactly once, so the
  /// call always ships the full [_desk] even when a chip is showing a subset.
  ///
  /// Reordering inside a filtered view keeps the rows the filter hides exactly
  /// where they were: the visible desk rows are moved among THEIR OWN positions
  /// in [_desk], and every hidden row stays at its index. Optimistic like the
  /// other desk actions — the list rolls back and toasts if the write fails.
  Future<void> _reorder(
    List<ModeratorQuestion> visible,
    int oldIndex,
    int newIndex,
  ) async {
    // Only desk rows carry a handle; rejected rows are appended on the الكل tab
    // and are not part of the running order.
    final deskIds = _desk.map((q) => q.id).toSet();
    final visibleDesk = visible.where((q) => deskIds.contains(q.id)).toList();
    if (visibleDesk.isEmpty || oldIndex >= visibleDesk.length) {
      return;
    }
    // `onReorderItem` already reports the FINAL position (unlike the
    // deprecated `onReorder`'s insert-before index). Clamp it into the desk
    // block so a drop past the last desk row — the rejected rows trailing the
    // الكل tab — lands at the end of the running order rather than nowhere.
    final target = newIndex.clamp(0, visibleDesk.length - 1);
    if (target == oldIndex) {
      return;
    }

    final movedVisible = <ModeratorQuestion>[...visibleDesk];
    movedVisible.insert(target, movedVisible.removeAt(oldIndex));

    // Write the moved sequence back into the positions the visible rows held in
    // the full desk; untouched (filtered-out) rows keep their slots.
    final visibleIds = visibleDesk.map((q) => q.id).toSet();
    final slots = <int>[];
    for (var i = 0; i < _desk.length; i++) {
      if (visibleIds.contains(_desk[i].id)) {
        slots.add(i);
      }
    }
    final next = <ModeratorQuestion>[..._desk];
    for (var i = 0; i < slots.length; i++) {
      next[slots[i]] = movedVisible[i];
    }

    final before = _desk;
    _apply(desk: next);
    try {
      await ref.read(moderationRepositoryProvider).reorder(
            widget.sessionId,
            next.map((q) => q.id).toList(growable: false),
          );
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      _apply(desk: before);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(AppL10n.of(context).moderatorReorderFailed)),
      );
    }
  }

  static List<ModeratorQuestion> _without(
    List<ModeratorQuestion> rows,
    String id,
  ) =>
      rows.where((r) => r.id != id).toList(growable: false);

  /// Swaps [next] in place when its row is present, otherwise appends it — so
  /// an optimistic update keeps the row's position in the queue.
  static List<ModeratorQuestion> _replace(
    List<ModeratorQuestion> rows,
    ModeratorQuestion next,
  ) {
    if (!rows.any((r) => r.id == next.id)) {
      return <ModeratorQuestion>[...rows, next];
    }
    return rows
        .map((r) => r.id == next.id ? next : r)
        .toList(growable: false);
  }

  /// Runs a repository action then reloads. Returns true on success; on failure
  /// surfaces a toast and returns false (callers roll back the optimistic row).
  Future<bool> _act(Future<void> Function() action) async {
    final messenger = ScaffoldMessenger.of(context);
    final l10n = AppL10n.of(context);
    try {
      await action();
      // The optimistic swap covers the window until this lands; the server
      // stays the source of truth for the final order and statuses.
      await _refresh();
      return true;
    } on ApiFailure {
      if (mounted) {
        messenger.showSnackBar(
          SnackBar(content: Text(l10n.moderatorActionFailed)),
        );
      }
      return false;
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: SafeArea(
        bottom: false,
        child: Column(
          children: <Widget>[
            ModeratorDeskHeader(
              title: l10n.moderatorDeskTitle,
              badgeLabel: l10n.moderatorBadge,
              onBack: () => backOrHome(context),
            ),
            Expanded(child: _body(l10n)),
          ],
        ),
      ),
    );
  }

  Widget _body(AppL10n l10n) {
    return ref.watch(moderatorQueuesProvider(widget.sessionId)).when(
      loading: () => const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      ),
      error: (error, _) {
        // The 403 branch too: a moderator assigned to the session after this
        // screen opened would otherwise be stuck on it with no way to re-check
        // (D-405), which is why BOTH failures stay refreshable.
        final forbidden = error is ApiFailure && error.httpStatus == 403;
        return SimfRefreshableMessage(
          onRefresh: _refresh,
          child: forbidden
              ? SimfEmptyState(
                  icon: Icons.lock_outline,
                  message: l10n.moderatorForbidden,
                )
              : SimfErrorState(
                  message: l10n.moderatorError,
                  retryLabel: l10n.retryLabel,
                  onRetry: () =>
                      ref.invalidate(moderatorQueuesProvider(widget.sessionId)),
                ),
        );
      },
      data: (queues) => ModeratorDesk(
        l10n: l10n,
        desk: queues.desk,
        rejected: queues.rejected,
        filter: _filter,
        onFilterChanged: (f) => setState(() => _filter = f),
        onRefresh: _refresh,
        onReorder: (rows, oldIndex, newIndex) =>
            unawaited(_reorder(rows, oldIndex, newIndex)),
        onReject: (q) => unawaited(_reject(q)),
        onToggleAnswered: (q) => unawaited(_toggleAnswered(q)),
        onPush: (q) => unawaited(_push(q)),
      ),
    );
  }
}
