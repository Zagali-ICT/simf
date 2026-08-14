import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/requests/widgets/request_action_row.dart';

/// Bi-Meeting rework — the اللقاءات الثنائية top action area. The owner's two
/// request buttons — **"طلب مقابلة متحدث"** (speaker) and **"طلب اجتماع وفد"**
/// (delegation) — each shown only when the signed-in user holds the matching
/// per-user flag ([showSpeaker] / [showDelegation]); the entitled request pills
/// sit on one row above the gold **"السجل"** history pill. Reuses the shared
/// [RequestActionButton] so the meetings + requests-history pages keep one
/// control.
class MeetingActionRow extends StatelessWidget {
  const MeetingActionRow({
    required this.l10n,
    required this.showSpeaker,
    required this.showDelegation,
    required this.onRequestSpeaker,
    required this.onRequestDelegation,
    required this.onHistory,
    super.key,
  });

  final AppL10n l10n;
  final bool showSpeaker;
  final bool showDelegation;
  final VoidCallback onRequestSpeaker;
  final VoidCallback onRequestDelegation;
  final VoidCallback onHistory;

  @override
  Widget build(BuildContext context) {
    // The Figma lays the pills left→right as a fixed LTR control — force LTR so
    // the RTL shell doesn't mirror it, matching the frame.
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          if (showSpeaker || showDelegation) ...<Widget>[
            Row(
              children: <Widget>[
                if (showSpeaker)
                  Expanded(
                    child: RequestActionButton(
                      label: l10n.requestSpeakerMeeting,
                      asset: AppAssets.requestNew,
                      active: false,
                      onTap: onRequestSpeaker,
                    ),
                  ),
                if (showSpeaker && showDelegation)
                  const SizedBox(width: SimfTokens.space4),
                if (showDelegation)
                  Expanded(
                    child: RequestActionButton(
                      label: l10n.requestDelegationMeeting,
                      asset: AppAssets.requestNew,
                      active: false,
                      onTap: onRequestDelegation,
                    ),
                  ),
              ],
            ),
            const SizedBox(height: SimfTokens.space4),
          ],
          RequestActionButton(
            label: l10n.requestsTabLog,
            asset: AppAssets.requestLog,
            active: true,
            onTap: onHistory,
          ),
        ],
      ),
    );
  }
}
