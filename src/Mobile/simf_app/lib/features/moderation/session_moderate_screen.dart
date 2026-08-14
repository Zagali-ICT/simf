import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';
import 'package:simf_app/features/moderation/data/moderation_repository.dart';
import 'package:simf_app/features/moderation/widgets/moderator_filter_bar.dart';
import 'package:simf_app/features/moderation/widgets/moderator_header.dart';
import 'package:simf_app/features/moderation/widgets/moderator_question_card.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Moderator (محاور) per-session Q&A desk — Figma 1461:12227 (D-405 / D-509).
///
/// Lists the question queue for [sessionId] with the five filter chips
/// (الكل / جديد / الأسئلة المقبولة / تمت الإجابة / مرفوض) and the three
/// per-question actions: **مرفوض** (reject → `hide`, the moderator's tool for an
/// invalid / not-in-hall question — owner directive), **يتم الإجابة** (push on
/// stage), and **تمت الإجابة** (mark answered).
///
/// DEF-MOD-001 / DEF-MOD-002 — every chip is backed by the PERSISTED
/// `QuestionStatus`: the desk reads the working queue (Approved + Answered) and,
/// separately, its own rejected (Hidden) rows. Marking answered and rejecting
/// both hit real endpoints, so nothing is lost when the moderator leaves the
/// screen, the app restarts, or a co-moderator opens the same desk on another
/// device — and a mis-clicked reject can be restored from the مرفوض tab. Each
/// action updates the row optimistically and rolls back if the call fails.
///
/// Authority is the per-session `SessionModerator` grant (or Administrator),
/// **not** the mobile `AppRole.moderator` — a moderator without the grant gets
/// a 403, shown as the "not a moderator for this session" state.
class SessionModerateScreen extends ConsumerStatefulWidget {
  const SessionModerateScreen({required this.sessionId, super.key});

  final String sessionId;

  @override
  ConsumerState<SessionModerateScreen> createState() =>
      _SessionModerateScreenState();
}

class _SessionModerateScreenState extends ConsumerState<SessionModerateScreen> {
  bool _loading = true;
  bool _error = false;
  bool _forbidden = false;
  // The working desk (Approved + Answered) and the rejected (Hidden) bucket —
  // both server-owned, both refetched on every reload.
  List<ModeratorQuestion> _desk = const <ModeratorQuestion>[];
  List<ModeratorQuestion> _rejected = const <ModeratorQuestion>[];
  ModeratorQueueFilter _filter = ModeratorQueueFilter.all;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
      _forbidden = false;
    });
    try {
      final repo = ref.read(moderationRepositoryProvider);
      final desk = await repo.getQueue(widget.sessionId);
      final rejected = await repo.getQueue(
        widget.sessionId,
        status: ModeratorQuestionStatus.hidden,
      );
      if (!mounted) {
        return;
      }
      setState(() {
        _desk = desk;
        _rejected = rejected;
        _loading = false;
      });
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      setState(() {
        // 403 = not granted as a moderator for this session (D-405).
        _forbidden = e.httpStatus == 403;
        _error = e.httpStatus != 403;
        _loading = false;
      });
    }
  }

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
    setState(() {
      _desk = _without(_desk, q.id);
      _rejected = <ModeratorQuestion>[
        ..._without(_rejected, q.id),
        q.withStatus(ModeratorQuestionStatus.hidden),
      ];
    });
    final ok = await _act(
      () => ref.read(moderationRepositoryProvider).setHidden(
            widget.sessionId,
            q.id,
            isHidden: true,
          ),
    );
    if (!ok && mounted) {
      setState(() {
        _desk = deskBefore;
        _rejected = rejectedBefore;
      });
    }
  }

  /// Restore a previously rejected question (un-hide) back into the desk.
  /// DEF-MOD-002 — the rejected rows come from the server, so a mis-click stays
  /// recoverable after leaving the screen.
  Future<bool> _restore(ModeratorQuestion q) async {
    final deskBefore = _desk;
    final rejectedBefore = _rejected;
    setState(() {
      _rejected = _without(_rejected, q.id);
      _desk = <ModeratorQuestion>[
        ..._without(_desk, q.id),
        q.withStatus(ModeratorQuestionStatus.approved),
      ];
    });
    final ok = await _act(
      () => ref.read(moderationRepositoryProvider).setHidden(
            widget.sessionId,
            q.id,
            isHidden: false,
          ),
    );
    if (!ok && mounted) {
      setState(() {
        _desk = deskBefore;
        _rejected = rejectedBefore;
      });
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
    setState(() {
      _desk = _replace(
        _desk,
        q.withStatus(
          isAnswered
              ? ModeratorQuestionStatus.answered
              : ModeratorQuestionStatus.approved,
        ),
      );
    });
    final ok = await _act(
      () => ref.read(moderationRepositoryProvider).setAnswered(
            widget.sessionId,
            q.id,
            isAnswered: isAnswered,
          ),
    );
    if (!ok && mounted) {
      setState(() => _desk = deskBefore);
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
    setState(() => _desk = next);
    try {
      await ref.read(moderationRepositoryProvider).reorder(
            widget.sessionId,
            next.map((q) => q.id).toList(growable: false),
          );
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() => _desk = before);
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

  /// Swaps [next] in place when its row is present, otherwise appends it — so an
  /// optimistic update keeps the row's position in the queue.
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
      await _load();
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
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    // The 403 branch too: a moderator assigned to the session after this
    // screen opened would otherwise be stuck on it with no way to re-check.
    if (_forbidden) {
      return SimfRefreshableMessage(
        onRefresh: _load,
        child: SimfEmptyState(
          icon: Icons.lock_outline,
          message: l10n.moderatorForbidden,
        ),
      );
    }
    if (_error) {
      return SimfRefreshableMessage(
        onRefresh: _load,
        child: SimfErrorState(
          message: l10n.moderatorError,
          retryLabel: l10n.retryLabel,
          onRetry: () => unawaited(_load()),
        ),
      );
    }
    final rows = filterModeratorQueue(_desk, _filter, rejected: _rejected);
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
                  onRefresh: _load,
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
