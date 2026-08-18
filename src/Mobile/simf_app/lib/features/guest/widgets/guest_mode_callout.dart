import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

class GuestModeCallout extends StatelessWidget {
  const GuestModeCallout({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space4,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.accent.withValues(alpha: 0.06),
        border: Border.all(color: SimfTokens.accent),
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            l10n.guestModeBrowseBody,
            style: const TextStyle(
              color: SimfTokens.txtSecondary,
              fontSize: SimfTokens.textSm,
              height: SimfTokens.guestModeScreenHeightSm,
            ),
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(
            l10n.guestModeSignInBody,
            style: const TextStyle(
              color: SimfTokens.txtSecondary,
              fontSize: SimfTokens.textSm,
              height: SimfTokens.guestModeScreenHeightSm,
            ),
          ),
        ],
      ),
    );
  }
}
