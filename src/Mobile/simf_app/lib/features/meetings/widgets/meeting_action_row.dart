import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../requests/widgets/request_action_row.dart';

/// The اللقاءات الثنائية top action row (Figma 1408:9736): two equal pills —
/// **"طلب جديد"** (beige outline → opens the new-meeting sheet) and **"السجل"**
/// (gold-filled → opens the requests-history page). Reuses the shared
/// [RequestActionButton] pill so the meetings page and the requests-history page
/// keep an identical control, rather than a page-local copy.
///
/// The whole meetings page is VIP-only, so "طلب جديد" always shows here (unlike
/// the history page, where it is VIP-gated).
class MeetingActionRow extends StatelessWidget {
  const MeetingActionRow({
    required this.l10n,
    required this.onNew,
    required this.onHistory,
    super.key,
  });

  final AppL10n l10n;
  final VoidCallback onNew;
  final VoidCallback onHistory;

  @override
  Widget build(BuildContext context) {
    // The Figma lays this row left→right (طلب جديد · السجل) as a fixed LTR
    // control — force LTR so the RTL shell doesn't mirror it, matching the frame.
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Row(
        children: <Widget>[
          Expanded(
            child: RequestActionButton(
              label: l10n.requestNew,
              asset: AppAssets.requestNew,
              active: false,
              onTap: onNew,
            ),
          ),
          const SizedBox(width: SimfTokens.space4),
          Expanded(
            child: RequestActionButton(
              label: l10n.requestsTabLog,
              asset: AppAssets.requestLog,
              active: true,
              onTap: onHistory,
            ),
          ),
        ],
      ),
    );
  }
}
