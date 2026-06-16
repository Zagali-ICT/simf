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

/// Moderator (محاور) per-session Q&A desk — Figma 758:5307 (D-405).
///
/// Lists the **approved** question queue for [sessionId] and lets the moderator
/// mark a question **on stage** (يتم الإجابة → `push`) or **reject** it
/// (مرفوض → `hide`). Backend-faithful subset: the API has no distinct
/// "answered" status, so the chips are الكل / جديد / يتم الإجابة only (the
/// Figma's تمت الإجابة state is flagged for backend follow-up).
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

  Future<void> _push(ModeratorQuestion q) => _act(
        () => ref
            .read(moderationRepositoryProvider)
            .push(widget.sessionId, q.id),
      );

  Future<void> _reject(ModeratorQuestion q) => _act(
        () => ref.read(moderationRepositoryProvider).setHidden(
              widget.sessionId,
              q.id,
              isHidden: true,
            ),
      );

  Future<void> _act(Future<void> Function() action) async {
    final messenger = ScaffoldMessenger.of(context);
    final l10n = AppL10n.of(context);
    try {
      await action();
      await _load();
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.moderatorActionFailed)),
      );
    }
  }

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
    final rows = filterModeratorQueue(_all, _filter);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        _FilterBar(
          l10n: l10n,
          filter: _filter,
          all: _all.length,
          fresh: filterModeratorQueue(_all, ModeratorQueueFilter.fresh).length,
          onStage:
              filterModeratorQueue(_all, ModeratorQueueFilter.onStage).length,
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
                      onPush: () => unawaited(_push(rows[i])),
                      onReject: () => unawaited(_reject(rows[i])),
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
        borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
        border: Border.all(color: SimfTokens.accent),
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: SimfTokens.accent,
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
    required this.all,
    required this.fresh,
    required this.onStage,
    required this.onChanged,
  });

  final AppL10n l10n;
  final ModeratorQueueFilter filter;
  final int all;
  final int fresh;
  final int onStage;
  final ValueChanged<ModeratorQueueFilter> onChanged;

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
          _Chip(
            label: l10n.moderatorChipOnStage,
            count: onStage,
            active: filter == ModeratorQueueFilter.onStage,
            onTap: () => onChanged(ModeratorQueueFilter.onStage),
          ),
          const SizedBox(width: SimfTokens.space2),
          _Chip(
            label: l10n.moderatorChipNew,
            count: fresh,
            active: filter == ModeratorQueueFilter.fresh,
            onTap: () => onChanged(ModeratorQueueFilter.fresh),
          ),
          const SizedBox(width: SimfTokens.space2),
          _Chip(
            label: l10n.moderatorChipAll,
            count: all,
            active: filter == ModeratorQueueFilter.all,
            onTap: () => onChanged(ModeratorQueueFilter.all),
          ),
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
    required this.onPush,
    required this.onReject,
  });

  final AppL10n l10n;
  final ModeratorQuestion question;
  final VoidCallback onPush;
  final VoidCallback onReject;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: question.isOnStage ? SimfTokens.accent : SimfTokens.beigeBorder,
          width: question.isOnStage ? 1 : 0.2,
        ),
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
                  filled: false,
                  onTap: onReject,
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: _ActionButton(
                  label: l10n.moderatorActionOnStage,
                  icon: Icons.access_time,
                  color: SimfTokens.accent,
                  filled: question.isOnStage,
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
        decoration: BoxDecoration(
          color: filled ? color : Colors.transparent,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          border: Border.all(color: color),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Text(
              label,
              style: TextStyle(
                color: filled ? SimfTokens.navy : color,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textSm,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Icon(icon, size: 16, color: filled ? SimfTokens.navy : color),
          ],
        ),
      ),
    );
  }
}
