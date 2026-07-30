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
/// Lists the **approved** question queue for [sessionId] with the five filter
/// chips (الكل / جديد / الأسئلة المقبولة / تمت الإجابة / مرفوض) and the three
/// per-question actions: **مرفوض** (reject → `hide`, the moderator's tool for an
/// invalid / not-in-hall question — owner directive), **يتم الإجابة** (push on
/// stage), and **تمت الإجابة** (mark answered).
///
/// Backend-faithful mapping: the API exposes only Approved/Hidden + a `push`
/// flag, so **reject** and **on-stage** hit the real endpoints, while
/// **answered** and the **rejected list** are moderator-session-local (there is
/// no distinct "answered" status, and a hidden row drops out of the approved
/// queue) — see [ModeratorQueueFilter].
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
  List<ModeratorQuestion> _all = const <ModeratorQuestion>[];
  // Session-local moderator state (no backend status — see the class doc):
  // questions marked answered, and the rows rejected this session (kept so the
  // مرفوض tab still lists them after they drop out of the approved queue).
  final Set<String> _answered = <String>{};
  final List<ModeratorQuestion> _rejected = <ModeratorQuestion>[];
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
      final queue =
          await ref.read(moderationRepositoryProvider).getQueue(widget.sessionId);
      if (!mounted) {
        return;
      }
      setState(() {
        _all = queue;
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

  bool _isRejected(ModeratorQuestion q) => _rejected.any((r) => r.id == q.id);

  /// يتم الإجابة — push the question on stage (real). If it was rejected this
  /// session, restore it first.
  Future<void> _push(ModeratorQuestion q) async {
    if (_isRejected(q)) {
      await _restore(q);
      // _restore awaits a network round-trip and _act reads context on entry.
      if (!mounted) {
        return;
      }
    }
    await _act(
      () => ref.read(moderationRepositoryProvider).push(widget.sessionId, q.id),
    );
  }

  /// مرفوض — reject (real `hide`). The row is kept locally so it still lists
  /// under the مرفوض tab; on the next reload the approved queue excludes it.
  Future<void> _reject(ModeratorQuestion q) async {
    setState(() {
      _answered.remove(q.id);
      if (!_isRejected(q)) {
        _rejected.add(q);
      }
    });
    final ok = await _act(
      () => ref.read(moderationRepositoryProvider).setHidden(
            widget.sessionId,
            q.id,
            isHidden: true,
          ),
    );
    if (!ok && mounted) {
      // The reject failed — undo the optimistic local move.
      setState(() => _rejected.removeWhere((r) => r.id == q.id));
    }
  }

  /// Restore a previously rejected question (un-hide) back into the queue.
  Future<void> _restore(ModeratorQuestion q) async {
    setState(() => _rejected.removeWhere((r) => r.id == q.id));
    await _act(
      () => ref.read(moderationRepositoryProvider).setHidden(
            widget.sessionId,
            q.id,
            isHidden: false,
          ),
    );
  }

  /// تمت الإجابة — mark the question answered (session-local, toggles). If it was
  /// rejected, restore it first so it lands back in the live queue as answered.
  Future<void> _toggleAnswered(ModeratorQuestion q) async {
    if (_isRejected(q)) {
      await _restore(q);
    }
    if (!mounted) {
      return;
    }
    setState(() {
      if (!_answered.add(q.id)) {
        _answered.remove(q.id);
      }
    });
  }

  /// Runs a repository action then reloads. Returns true on success; on failure
  /// surfaces a toast and returns false (callers can roll back optimistic state).
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

  int _count(ModeratorQueueFilter filter) => filterModeratorQueue(
        _all,
        filter,
        answeredIds: _answered,
        rejected: _rejected,
      ).length;

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
    final rows = filterModeratorQueue(
      _all,
      _filter,
      answeredIds: _answered,
      rejected: _rejected,
    );
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
                      answered: _answered.contains(rows[i].id),
                      rejected: _isRejected(rows[i]),
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
