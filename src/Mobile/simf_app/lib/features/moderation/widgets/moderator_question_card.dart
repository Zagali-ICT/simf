import 'package:flutter/material.dart';
import 'package:intl/intl.dart' show DateFormat;

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';
import 'package:simf_app/features/moderation/widgets/moderator_action_button.dart';

final DateFormat _hm = DateFormat('hh:mm a');

/// One moderator question card (Figma 1462:12236): a navy card with an 8px gold
/// TOP border, a header row (time left, name + gold initial-avatar right), a
/// bordered question box, and three large action buttons (reject / answered /
/// on-stage).
class ModeratorQuestionCard extends StatelessWidget {
  const ModeratorQuestionCard({
    required this.l10n,
    required this.question,
    required this.answered,
    required this.rejected,
    required this.onReject,
    required this.onAnswered,
    required this.onPush,
    this.dragHandleIndex,
    super.key,
  });

  final AppL10n l10n;
  final ModeratorQuestion question;
  final bool answered;
  final bool rejected;
  final VoidCallback onReject;
  final VoidCallback onAnswered;
  final VoidCallback onPush;

  /// FR-MOD-003 — the row's index in the desk list, which turns the card's
  /// leading glyph into a drag handle for `PUT …/questions/reorder`. Null on a
  /// row that cannot be reordered (a rejected question is off the desk, so it
  /// has no place in the running order) — the glyph is then omitted entirely
  /// rather than rendered dead.
  final int? dragHandleIndex;

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
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: const BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
        border: Border(
          top: BorderSide(
            color: SimfTokens.accent,
            width: SimfTokens.moderatorCardTopBorderWidth,
          ),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: <Widget>[
          Row(
            children: <Widget>[
              // FR-MOD-003 — the drag handle sits at the row's leading edge, so
              // it mirrors with the rest of the card in RTL. A vertical reorder
              // list has no left/right semantics to invert: "up" is earlier in
              // the running order in both languages.
              if (dragHandleIndex != null) ...<Widget>[
                ReorderableDragStartListener(
                  index: dragHandleIndex!,
                  child: Semantics(
                    // Its OWN node: without container the handle's name is
                    // merged into the card's, so a screen reader reads the
                    // question text and the handle as one blob and the control
                    // has no name of its own to announce.
                    container: true,
                    label: l10n.moderatorReorderHandle,
                    button: true,
                    child: const Padding(
                      padding: EdgeInsetsDirectional.only(
                        end: SimfTokens.space3,
                      ),
                      child: Icon(
                        Icons.drag_handle,
                        color: SimfTokens.accent,
                      ),
                    ),
                  ),
                ),
              ],
              Text(
                _hm.format(saudiOf(question.createdAt)),
                style: SimfTokens.labelBeigeSemibold24,
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
                      style: SimfTokens.labelWhiteBoldHero,
                    ),
                    if (question.recipient == QuestionRecipient.host)
                      Text(
                        l10n.moderatorToHost,
                        style: SimfTokens.labelGoldTitle,
                      ),
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space5),
              Container(
                width: SimfTokens.moderatorQuestionCardWidth,
                height: SimfTokens.moderatorQuestionCardHeight,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: SimfTokens.accent,
                  borderRadius: BorderRadius.circular(SimfTokens.radiusLg),
                ),
                child: Text(
                  _initials(question.submitterName),
                  style: SimfTokens.labelWhiteExtraBoldHero,
                ),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space6),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(SimfTokens.space5),
            decoration: BoxDecoration(
              color: SimfTokens.black.withValues(
                alpha: SimfTokens.moderatorScrimOpacity,
              ),
              border: Border.all(
                color: SimfTokens.accent,
                width: SimfTokens.borderThick,
              ),
              borderRadius: const BorderRadius.only(
                topRight: Radius.circular(SimfTokens.radiusLg),
                topLeft: Radius.circular(SimfTokens.radius),
                bottomLeft: Radius.circular(SimfTokens.radiusLg),
                bottomRight: Radius.circular(SimfTokens.radiusLg),
              ),
            ),
            child: Text(
              question.questionText,
              textAlign: TextAlign.start,
              style: SimfTokens.labelWhiteBoldHeroTall,
            ),
          ),
          const SizedBox(height: SimfTokens.space6),
          Row(
            children: <Widget>[
              Expanded(
                child: ModeratorActionButton(
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
                child: ModeratorActionButton(
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
                child: ModeratorActionButton(
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
