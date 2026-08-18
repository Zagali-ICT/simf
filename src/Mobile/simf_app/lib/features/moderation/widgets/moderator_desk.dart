import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';
import 'package:simf_app/features/moderation/widgets/moderator_filter_bar.dart';
import 'package:simf_app/features/moderation/widgets/moderator_question_card.dart';

/// The moderator desk itself: the filter bar plus the reorderable question
/// queue underneath it.
///
/// Extracted from `session_moderate_screen.dart`, which had grown past the
/// 400-line limit and so tripped SIMF-C3 on its `_buildDesk` method. The screen
/// keeps every action and all of the optimistic state; this widget is pure
/// presentation and reports back through callbacks, which is why it can be a
/// `StatelessWidget` despite driving a reorderable list.
class ModeratorDesk extends StatelessWidget {
  const ModeratorDesk({
    required this.l10n,
    required this.desk,
    required this.rejected,
    required this.filter,
    required this.onFilterChanged,
    required this.onRefresh,
    required this.onReorder,
    required this.onReject,
    required this.onToggleAnswered,
    required this.onPush,
    super.key,
  });

  final AppL10n l10n;

  /// The two buckets, passed as plain lists rather than the `ModeratorQueues`
  /// holder, so this widget stays independent of the desk's provider.
  final List<ModeratorQuestion> desk;
  final List<ModeratorQuestion> rejected;

  final ModeratorQueueFilter filter;
  final ValueChanged<ModeratorQueueFilter> onFilterChanged;
  final Future<void> Function() onRefresh;

  /// Receives the VISIBLE rows alongside the indices, because the running order
  /// the server stores is computed from what the moderator can actually see.
  final void Function(List<ModeratorQuestion> rows, int oldIndex, int newIndex)
      onReorder;
  final ValueChanged<ModeratorQuestion> onReject;
  final ValueChanged<ModeratorQuestion> onToggleAnswered;
  final ValueChanged<ModeratorQuestion> onPush;

  @override
  Widget build(BuildContext context) {
    final rows = filterModeratorQueue(desk, filter, rejected: rejected);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        ModeratorFilterBar(
          l10n: l10n,
          filter: filter,
          counts: moderatorQueueCounts(desk, rejected: rejected),
          onChanged: onFilterChanged,
        ),
        Expanded(
          child: rows.isEmpty
              ? SimfEmptyState(
                  icon: Icons.forum_outlined,
                  message: l10n.moderatorEmpty,
                )
              : SimfPullToRefresh(
                  onRefresh: onRefresh,
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
                        onReorder(rows, oldIndex, newIndex),
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
                        onReject: () => onReject(rows[i]),
                        onAnswered: () => onToggleAnswered(rows[i]),
                        onPush: () => onPush(rows[i]),
                      ),
                    ),
                  ),
                ),
        ),
      ],
    );
  }
}
