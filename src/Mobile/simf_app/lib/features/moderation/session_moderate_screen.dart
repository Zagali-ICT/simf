import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart' show DateFormat;
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import 'data/moderation_models.dart';
import 'data/moderation_repository.dart';

final DateFormat _hm = DateFormat('HH:mm');

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
      backgroundColor: SimfTokens.navy,
      appBar: AppBar(
        leading: const SimfBackButton(),
        backgroundColor: SimfTokens.navy,
        foregroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
        title: Text(l10n.moderatorDeskTitle),
        actions: <Widget>[
          Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: SimfTokens.space4,
              vertical: SimfTokens.space3,
            ),
            child: _RolePill(label: l10n.moderatorBadge),
          ),
        ],
      ),
      body: SafeArea(top: false, child: _body(l10n)),
    );
  }

  Widget _body(AppL10n l10n) {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    if (_forbidden) {
      return KsaEmptyState(
        icon: Icons.lock_outline,
        message: l10n.moderatorForbidden,
      );
    }
    if (_error) {
      return KsaErrorState(
        message: l10n.moderatorError,
        retryLabel: l10n.retryLabel,
        onRetry: () => unawaited(_load()),
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
        _FilterBar(
          l10n: l10n,
          filter: _filter,
          counts: <ModeratorQueueFilter, int>{
            for (final f in ModeratorQueueFilter.values) f: _count(f),
          },
          onChanged: (f) => setState(() => _filter = f),
        ),
        Expanded(
          child: rows.isEmpty
              ? KsaEmptyState(
                  icon: Icons.forum_outlined,
                  message: l10n.moderatorEmpty,
                )
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView.separated(
                    padding: const EdgeInsets.all(SimfTokens.space4),
                    itemCount: rows.length,
                    separatorBuilder: (_, __) =>
                        const SizedBox(height: SimfTokens.space3),
                    itemBuilder: (context, i) => _QuestionCard(
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

class _RolePill extends StatelessWidget {
  const _RolePill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: SimfTokens.navy,
          fontWeight: FontWeight.w700,
          fontSize: SimfTokens.textSm,
        ),
      ),
    );
  }
}

class _FilterBar extends StatelessWidget {
  const _FilterBar({
    required this.l10n,
    required this.filter,
    required this.counts,
    required this.onChanged,
  });

  final AppL10n l10n;
  final ModeratorQueueFilter filter;
  final Map<ModeratorQueueFilter, int> counts;
  final ValueChanged<ModeratorQueueFilter> onChanged;

  String _label(ModeratorQueueFilter f) {
    switch (f) {
      case ModeratorQueueFilter.all:
        return l10n.moderatorChipAll;
      case ModeratorQueueFilter.fresh:
        return l10n.moderatorChipNew;
      case ModeratorQueueFilter.accepted:
        return l10n.moderatorChipAccepted;
      case ModeratorQueueFilter.answered:
        return l10n.moderatorChipAnswered;
      case ModeratorQueueFilter.rejected:
        return l10n.moderatorChipRejected;
    }
  }

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space3,
      ),
      child: Row(
        children: <Widget>[
          for (final f in ModeratorQueueFilter.values) ...<Widget>[
            _Chip(
              label: _label(f),
              count: counts[f] ?? 0,
              active: filter == f,
              onTap: () => onChanged(f),
            ),
            const SizedBox(width: SimfTokens.space2),
          ],
        ],
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({
    required this.label,
    required this.count,
    required this.active,
    required this.onTap,
  });

  final String label;
  final int count;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space2,
        ),
        decoration: BoxDecoration(
          color: active ? SimfTokens.accent : SimfTokens.navyDeep,
          borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
          border: Border.all(
            color: active ? SimfTokens.accent : SimfTokens.beigeBorder,
            width: 0.5,
          ),
        ),
        child: Row(
          children: <Widget>[
            Text(
              label,
              style: TextStyle(
                color: active ? SimfTokens.navy : Colors.white,
                fontWeight: FontWeight.w600,
                fontSize: SimfTokens.textSm,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Container(
              padding: const EdgeInsets.symmetric(
                horizontal: SimfTokens.space2,
                vertical: 1,
              ),
              decoration: BoxDecoration(
                color: active
                    ? SimfTokens.navy.withValues(alpha: 0.15)
                    : SimfTokens.navy,
                borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
              ),
              child: Text(
                '$count',
                style: TextStyle(
                  color: active ? SimfTokens.navy : SimfTokens.accent,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textXs,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _QuestionCard extends StatelessWidget {
  const _QuestionCard({
    required this.l10n,
    required this.question,
    required this.answered,
    required this.rejected,
    required this.onReject,
    required this.onAnswered,
    required this.onPush,
  });

  final AppL10n l10n;
  final ModeratorQuestion question;
  final bool answered;
  final bool rejected;
  final VoidCallback onReject;
  final VoidCallback onAnswered;
  final VoidCallback onPush;

  Color get _statusColor {
    if (rejected) {
      return SimfTokens.danger;
    }
    if (answered) {
      return SimfTokens.success;
    }
    if (question.isOnStage) {
      return SimfTokens.warning;
    }
    return SimfTokens.accent;
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(color: _statusColor, width: 1),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Text(
                _hm.format(question.createdAt.toLocal()),
                style: const TextStyle(
                  color: SimfTokens.beigeBorder,
                  fontSize: SimfTokens.textXs,
                ),
              ),
              const Spacer(),
              Expanded(
                flex: 4,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: <Widget>[
                    Text(
                      question.submitterName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      textAlign: TextAlign.end,
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w700,
                        fontSize: SimfTokens.textSm,
                      ),
                    ),
                    if (question.recipient == QuestionRecipient.host)
                      Text(
                        l10n.moderatorToHost,
                        style: const TextStyle(
                          color: SimfTokens.accent,
                          fontSize: SimfTokens.textXs,
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              KsaAvatar(name: question.submitterName, size: 40),
            ],
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(
            question.questionText,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textMd,
              height: 1.4,
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          Row(
            children: <Widget>[
              Expanded(
                child: _ActionButton(
                  label: l10n.moderatorActionReject,
                  icon: Icons.close,
                  color: SimfTokens.danger,
                  filled: rejected,
                  onTap: onReject,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: _ActionButton(
                  label: l10n.moderatorActionAnswered,
                  icon: Icons.check,
                  color: SimfTokens.success,
                  filled: answered,
                  onTap: onAnswered,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: _ActionButton(
                  label: l10n.moderatorActionOnStage,
                  icon: Icons.access_time,
                  color: SimfTokens.warning,
                  filled: question.isOnStage && !answered && !rejected,
                  onTap: onPush,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _ActionButton extends StatelessWidget {
  const _ActionButton({
    required this.label,
    required this.icon,
    required this.color,
    required this.filled,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final Color color;
  final bool filled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: Container(
        height: 40,
        alignment: Alignment.center,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
        decoration: BoxDecoration(
          color: filled ? color : Colors.transparent,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          border: Border.all(color: color),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Flexible(
              child: Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: filled ? Colors.white : color,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textXs,
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space1),
            Icon(icon, size: 14, color: filled ? Colors.white : color),
          ],
        ),
      ),
    );
  }
}
