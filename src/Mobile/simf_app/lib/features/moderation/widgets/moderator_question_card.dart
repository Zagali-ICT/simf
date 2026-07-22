import 'package:flutter/material.dart';
import 'package:intl/intl.dart' show DateFormat;

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/moderation_models.dart';
import 'moderator_action_button.dart';

final DateFormat _hm = DateFormat('HH:mm');

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
    super.key,
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
              Text(
                _hm.format(question.createdAt.toLocal()),
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
                width: 80,
                height: 80,
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
          // Question box — dark inset, gold border, asymmetric radii.
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(SimfTokens.space5),
            decoration: BoxDecoration(
              color: Colors.black.withValues(
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
