import 'package:flutter/material.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';

/// The four-step registration progress card (Figma 1701:3805–3822): a navy-80%
/// card with a right-aligned "المراحل" title and one right-aligned row per
/// stage — a white label with its completion marker at the inline end. Steps 1–2
/// are always complete; step 3 (team review) is the current step while Pending
/// and complete on Approved; step 4 (activation) completes on Approved.
class RegistrationStagesCard extends StatelessWidget {
  const RegistrationStagesCard({
    required this.status,
    required this.l10n,
    super.key,
  });

  final RegistrationStatus status;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final approved = status == RegistrationStatus.approved;
    final steps = <_Stage>[
      _Stage(l10n.stageDataSubmitted, _StageState.complete, 1),
      _Stage(l10n.stageEmailConfirmed, _StageState.complete, 2),
      _Stage(
        l10n.stageTeamReview,
        approved ? _StageState.complete : _StageState.current,
        3,
      ),
      _Stage(
        l10n.stageActivation,
        approved ? _StageState.complete : _StageState.future,
        4,
      ),
    ];
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navy.withValues(alpha: 0.8),
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      // crossAxisAlignment.start = the inline start (right under RTL), so the
      // title and every row hug the right edge like the frame.
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            l10n.stagesTitle,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textMd, // 14
            ),
          ),
          for (final step in steps)
            Padding(
              padding: const EdgeInsets.only(top: SimfTokens.space2),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    step.label,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: SimfTokens.textMd, // 14
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(width: SimfTokens.space2),
                  _StageMarker(step),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// The 18px stage marker: a gold done-check when complete, a gold ring while the
/// step is current, and a muted numbered circle while it is still ahead.
class _StageMarker extends StatelessWidget {
  const _StageMarker(this.step);

  final _Stage step;

  @override
  Widget build(BuildContext context) {
    switch (step.state) {
      case _StageState.complete:
        return const Icon(
          Icons.check_rounded,
          size: 18,
          color: SimfTokens.accent,
        );
      case _StageState.current:
        return Container(
          width: 18,
          height: 18,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            border: Border.all(color: SimfTokens.accent, width: 1.5),
          ),
          child: const Icon(Icons.circle, size: 8, color: SimfTokens.accent),
        );
      case _StageState.future:
        return Container(
          width: 18,
          height: 18,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            border: Border.all(color: SimfTokens.line),
          ),
          child: Text(
            '${step.number}',
            style: const TextStyle(
              color: SimfTokens.txtTertiary,
              fontSize: SimfTokens.textXs, // ~11
              fontWeight: FontWeight.w600,
            ),
          ),
        );
    }
  }
}

enum _StageState { complete, current, future }

class _Stage {
  const _Stage(this.label, this.state, this.number);

  final String label;
  final _StageState state;
  final int number;
}
