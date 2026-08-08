import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The "you are browsing as a guest" banner: a navy card with the gold
/// highlighted phrase inside the beige copy (frame node 512:1499).
class GuestBanner extends StatelessWidget {
  const GuestBanner({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space3,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.accent,
          width: SimfTokens.hairline,
        ),
      ),
      child: Text.rich(
        TextSpan(
          style: SimfTokens.bodyBeige,
          children: <InlineSpan>[
            TextSpan(text: l10n.guestBannerPrefix),
            TextSpan(
              text: l10n.guestBannerHighlight,
              style: const TextStyle(
                color: SimfTokens.accent,
                fontWeight: FontWeight.w600,
              ),
            ),
            TextSpan(text: l10n.guestBannerSuffix),
          ],
        ),
      ),
    );
  }
}
