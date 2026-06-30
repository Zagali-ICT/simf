import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart' show DateFormat;
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
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
      backgroundColor: SimfTokens.navySurface,
      body: SafeArea(
        bottom: false,
        child: Column(
          children: <Widget>[
            // Figma 1461:12565 — navy header block: back (left) + title (centre)
            // + role pill (right). Forced LTR so back sits on the left as drawn.
            Container(
              color: SimfTokens.navyHeader,
              padding: const EdgeInsets.fromLTRB(8, 4, 16, 16),
              child: Row(
                textDirection: TextDirection.ltr,
                children: <Widget>[
                  // Circular navy back button with a left chevron (Figma).
                  Padding(
                    padding: const EdgeInsets.all(8),
                    child: Material(
                      color: SimfTokens.navyDeep,
                      shape: const CircleBorder(),
                      child: InkWell(
                        customBorder: const CircleBorder(),
                        onTap: () => backOrHome(context),
                        child: const Padding(
                          padding: EdgeInsets.all(6),
                          child: Icon(
                            Icons.chevron_left,
                            color: Colors.white,
                            size: 26,
                          ),
                        ),
                      ),
                    ),
                  ),
                  Expanded(
                    child: Text(
                      l10n.moderatorDeskTitle,
                      textAlign: TextAlign.center,
                      textDirection: TextDirection.rtl,
                      style: const TextStyle(
                        fontSize: 28,
                        fontWeight: FontWeight.w700,
                        color: Colors.white,
                      ),
                    ),
                  ),
                  _RolePill(label: l10n.moderatorBadge),
                ],
              ),
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
              ? SimfEmptyState(
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
      padding: const EdgeInsets.all(8),
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: Colors.white,
          fontWeight: FontWeight.w700,
          fontSize: SimfTokens.textTitle,
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
    // Figma 805:1876 — five equal-width chips filling the row (not a scroll).
    final filters = ModeratorQueueFilter.values;
    return Padding(
      padding: const EdgeInsets.fromLTRB(24, 16, 24, 8),
      child: Row(
        children: <Widget>[
          for (int i = 0; i < filters.length; i++) ...<Widget>[
            if (i > 0) const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: _Chip(
                label: _label(filters[i]),
                count: counts[filters[i]] ?? 0,
                active: filter == filters[i],
                onTap: () => onChanged(filters[i]),
              ),
            ),
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
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: Container(
        height: 58,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
        decoration: BoxDecoration(
          color: active ? SimfTokens.accent : SimfTokens.navyDeep,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          border: Border.all(
            color: active ? SimfTokens.accent : SimfTokens.navyDisabledBorder,
            width: 1.18,
          ),
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
                  color: active ? Colors.white : SimfTokens.beigeBorder,
                  fontWeight: active ? FontWeight.w700 : FontWeight.w400,
                  fontSize: SimfTokens.textTitle,
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Container(
              width: 28,
              height: 28,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: active
                    ? SimfTokens.navy.withValues(alpha: 0.3)
                    : SimfTokens.navyDisabledBorder,
                borderRadius: BorderRadius.circular(5),
              ),
              child: Text(
                '$count',
                style: TextStyle(
                  color: active ? Colors.white : SimfTokens.accent,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textMd,
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

  static String _initials(String name) {
    final parts = name
        .trim()
        .split(RegExp(r'\s+'))
        .where((p) => p.isNotEmpty)
        .toList();
    if (parts.isEmpty) {
      return '؟';
    }
    if (parts.length == 1) {
      final p = parts.first;
      return p.length >= 2 ? p.substring(0, 2) : p;
    }
    return parts[0].substring(0, 1) + parts[1].substring(0, 1);
  }

  @override
  Widget build(BuildContext context) {
    // Figma 1462:12236 — navy card with an 8px gold TOP border, a header row
    // (time left, name + gold initial-avatar right), a bordered question box,
    // and three large action buttons.
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: const BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
        border: Border(top: BorderSide(color: SimfTokens.accent, width: 8)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: <Widget>[
          Row(
            children: <Widget>[
              Text(
                _hm.format(question.createdAt.toLocal()),
                style: const TextStyle(
                  color: SimfTokens.beigeBorder,
                  fontWeight: FontWeight.w600,
                  fontSize: SimfTokens.textHero - 4, // 24
                ),
              ),
              const Spacer(),
              Flexible(
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
                        fontSize: SimfTokens.textHero, // 28
                      ),
                    ),
                    if (question.recipient == QuestionRecipient.host)
                      Text(
                        l10n.moderatorToHost,
                        style: const TextStyle(
                          color: SimfTokens.accent,
                          fontSize: SimfTokens.textTitle,
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space5),
              Container(
                width: 80,
                height: 80,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: SimfTokens.accent,
                  borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
                ),
                child: Text(
                  _initials(question.submitterName),
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w800,
                    fontSize: SimfTokens.textHero,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space6),
          // Question box — dark inset, gold border, asymmetric radii.
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(SimfTokens.space5),
            decoration: BoxDecoration(
              color: Colors.black.withValues(alpha: 0.25),
              border: Border.all(color: SimfTokens.accent, width: 2),
              borderRadius: const BorderRadius.only(
                topRight: Radius.circular(16),
                topLeft: Radius.circular(8),
                bottomLeft: Radius.circular(16),
                bottomRight: Radius.circular(16),
              ),
            ),
            child: Text(
              question.questionText,
              textAlign: TextAlign.right,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textHero, // 28
                height: 1.5,
              ),
            ),
          ),
          const SizedBox(height: SimfTokens.space6),
          Row(
            children: <Widget>[
              Expanded(
                child: _ActionButton(
                  label: l10n.moderatorActionReject,
                  icon: Icons.close,
                  color: SimfTokens.qReject,
                  filled: rejected,
                  primary: false,
                  onTap: onReject,
                ),
              ),
              const SizedBox(width: SimfTokens.space4),
              Expanded(
                child: _ActionButton(
                  label: l10n.moderatorActionAnswered,
                  icon: Icons.check,
                  color: SimfTokens.qAnswered,
                  filled: answered,
                  primary: false,
                  onTap: onAnswered,
                ),
              ),
              const SizedBox(width: SimfTokens.space4),
              Expanded(
                child: _ActionButton(
                  label: l10n.moderatorActionOnStage,
                  icon: Icons.access_time,
                  color: SimfTokens.qStage,
                  filled: question.isOnStage && !answered && !rejected,
                  // The on-stage CTA is drawn solid amber in the frame.
                  primary: true,
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
    required this.primary,
    required this.onTap,
  });

  final String label;
  final IconData icon;
  final Color color;
  final bool filled;

  /// The on-stage action is drawn solid by default (Figma); reject/answered are
  /// outline until their state is active.
  final bool primary;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final solid = filled || primary;
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
      child: Container(
        height: 88,
        alignment: Alignment.center,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
        decoration: BoxDecoration(
          color: solid ? color : color.withValues(alpha: 0.1),
          borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
          border: Border.all(color: color, width: 2),
          boxShadow: primary
              ? <BoxShadow>[
                  BoxShadow(
                    color: color.withValues(alpha: 0.25),
                    blurRadius: 10,
                    offset: const Offset(0, 8),
                  ),
                ]
              : null,
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Icon(icon, size: 30, color: solid ? Colors.white : color),
            const SizedBox(width: SimfTokens.space3),
            Flexible(
              child: Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: solid ? Colors.white : color,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textHero - 4, // 24
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
