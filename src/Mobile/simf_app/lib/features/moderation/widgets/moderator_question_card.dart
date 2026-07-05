import 'package:flutter/material.dart';
import 'package:intl/intl.dart' show DateFormat;

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/moderation_models.dart';

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
              textAlign: TextAlign.start,
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
