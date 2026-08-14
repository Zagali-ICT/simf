import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';
import 'package:simf_app/features/moderation/data/moderation_repository.dart';
import 'package:simf_app/features/moderation/widgets/moderator_filter_bar.dart';
import 'package:simf_app/features/moderation/widgets/moderator_header.dart';
import 'package:simf_app/features/moderation/widgets/moderator_question_card.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Moderator (محاور) per-session Q&A desk — Figma 1461:12227 (D-405 / D-509).
///
/// Lists the question queue for `sessionId` with the five filter chips (الكل /
/// جديد / الأسئلة المقبولة / تمت الإجابة / مرفوض) and the three per-question
/// actions: **مرفوض** (reject → `hide`, the moderator's tool for an invalid /
/// not-in-hall question — owner directive), **يتم الإجابة** (push on stage),
/// and **تمت الإجابة** (mark answered).
///
/// DEF-MOD-001 / DEF-MOD-002 — every chip is backed by the PERSISTED
/// `QuestionStatus`: the desk reads the working queue (Approved + Answered)
/// and, separately, its own rejected (Hidden) rows. Marking answered and
/// rejecting both hit real endpoints, so nothing is lost when the moderator
/// leaves the screen, the app restarts, or a co-moderator opens the same desk
/// on another device — and a mis-clicked reject can be restored from the مرفوض
/// tab. Each action updates the row optimistically and rolls back if the call
/// fails.
///
/// Authority is the per-session `SessionModerator` grant (or Administrator),
/// **not** the mobile `AppRole.moderator` — a moderator without the grant gets
/// a 403, shown as the "not a moderator for this session" state.
///
/// Route: `RouteNames.sessionModerate`.
/// Data: [moderationRepositoryProvider].
/// Perf: lazy — builds children on demand (ListView.builder).
/// The moderator's two server-owned buckets: the working desk (Approved +
/// Answered) and the rejected (Hidden) rows.
@immutable
class ModeratorQueues {
  const ModeratorQueues({required this.desk, required this.rejected});

  const ModeratorQueues.empty()
      : desk = const <ModeratorQuestion>[],
        rejected = const <ModeratorQuestion>[];

  final List<ModeratorQuestion> desk;
  final List<ModeratorQuestion> rejected;
}

/// An `AsyncNotifier`, not a `FutureProvider`, because this desk is EDITED.
///
/// Every action is optimistic with rollback — approve, reject, restore,
/// answered and reorder all swap the rows first and undo the swap if the write
/// fails. A `FutureProvider` cannot be written to, and re-fetching after each
/// action would defeat the point of the optimism. [apply] is the seam: it
/// publishes a new value without a request, which is exactly what `setState`
/// used to do on the two lists.
class ModeratorQueuesNotifier
    extends AutoDisposeFamilyAsyncNotifier<ModeratorQueues, String> {
  @override
  Future<ModeratorQueues> build(String sessionId) async {
    final repo = ref.watch(moderationRepositoryProvider);
    final desk = await repo.getQueue(sessionId);
    final rejected = await repo.getQueue(
      sessionId,
      status: ModeratorQuestionStatus.hidden,
    );
    return ModeratorQueues(desk: desk, rejected: rejected);
  }

  /// Publishes an optimistic edit (or a rollback) without a fetch.
  void apply(ModeratorQueues next) =>
      state = AsyncValue<ModeratorQueues>.data(next);
}

final moderatorQueuesProvider = AsyncNotifierProvider.autoDispose
    .family<ModeratorQueuesNotifier, ModeratorQueues, String>(
  ModeratorQueuesNotifier.new,
);

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
      ref.read(moderatorQueuesProvider(widget.sessionId)).valueOrNull ??
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
      data: (queues) => _buildDesk(l10n, queues),
    );
  }

  Widget _buildDesk(AppL10n l10n, ModeratorQueues queues) {
    final rows = filterModeratorQueue(
      queues.desk,
      _filter,
      rejected: queues.rejected,
    );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        ModeratorFilterBar(
          l10n: l10n,
          filter: _filter,
          counts: moderatorQueueCounts(_desk, rejected: _rejected),
          onChanged: (f) => setState(() => _filter = f),
        ),
        Expanded(
          child: rows.isEmpty
              ? SimfEmptyState(
                  icon: Icons.forum_outlined,
                  message: l10n.moderatorEmpty,
                )
              : SimfPullToRefresh(
                  onRefresh: _refresh,
                  // FR-MOD-003 — the queue the moderator reads on stage is
                  // now orderable from the desk itself (the reorder endpoint
                  // had no interface at all). Handles are built per card, so
                  // a rejected row simply has none.
                  child: ReorderableListView.builder(
                    padding: const EdgeInsets.all(SimfTokens.space4),
                    physics: const AlwaysScrollableScrollPhysics(),
                    buildDefaultDragHandles: false,
                    itemCount: rows.length,
                    onReorderItem: (oldIndex, newIndex) =>
                        unawaited(_reorder(rows, oldIndex, newIndex)),
                    itemBuilder: (context, i) => Padding(
                      key: ValueKey<String>(rows[i].id),
                      padding: const EdgeInsets.only(
                        bottom: SimfTokens.space3,
                      ),
                      child: ModeratorQuestionCard(
                        l10n: l10n,
                        question: rows[i],
                        answered: rows[i].isAnswered,
                        rejected: rows[i].isRejected,
                        dragHandleIndex: rows[i].isRejected ? null : i,
                        onReject: () => unawaited(_reject(rows[i])),
                        onAnswered: () => unawaited(_toggleAnswered(rows[i])),
                        onPush: () => unawaited(_push(rows[i])),
                      ),
                    ),
                  ),
                ),
        ),
      ],
    );
  }
}
