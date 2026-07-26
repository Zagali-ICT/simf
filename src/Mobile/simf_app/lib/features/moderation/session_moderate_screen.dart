import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/moderation_models.dart';
import 'data/moderation_repository.dart';
import 'widgets/moderator_filter_bar.dart';
import 'widgets/moderator_header.dart';
import 'widgets/moderator_question_card.dart';

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

  int _count(ModeratorQueueFilter filter) =>
      filterModeratorQueue(_desk, filter, rejected: _rejected).length;

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
    if (_forbidden) {
      return SimfEmptyState(
        icon: Icons.lock_outline,
        message: l10n.moderatorForbidden,
      );
    }
    if (_error) {
      return SimfErrorState(
        message: l10n.moderatorError,
        retryLabel: l10n.retryLabel,
        onRetry: () => unawaited(_load()),
      );
    }
    final rows = filterModeratorQueue(_desk, _filter, rejected: _rejected);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        ModeratorFilterBar(
          l10n: l10n,
          filter: _filter,
          counts: <ModeratorQueueFilter, int>{
            for (final f in ModeratorQueueFilter.values) f: _count(f),
          },
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
                  child: ListView.separated(
                    padding: const EdgeInsets.all(SimfTokens.space4),
                    itemCount: rows.length,
                    separatorBuilder: (_, __) =>
                        const SizedBox(height: SimfTokens.space3),
                    itemBuilder: (context, i) => ModeratorQuestionCard(
                      l10n: l10n,
                      question: rows[i],
                      answered: rows[i].isAnswered,
                      rejected: rows[i].isRejected,
                      onReject: () => unawaited(_reject(rows[i])),
                      onAnswered: () => unawaited(_toggleAnswered(rows[i])),
                      onPush: () => unawaited(_push(rows[i])),
                    ),
                  ),
                ),
        ),
      ],
    );
  }
}
