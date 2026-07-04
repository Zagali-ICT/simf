import 'dart:async';

import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/confirm_external_link.dart';
import '../../../core/organization_profile/organization_profile.dart';
import 'contact_card_chrome.dart';

/// The "وسائل التواصل الاجتماعي" row (frame node 1388:7711): one bordered tap
/// box per set social link. Brand-accurate glyphs are pending a Figma asset
/// export — Material approximations are used until then.
class ContactSocialCard extends StatelessWidget {
  const ContactSocialCard({required this.social, super.key});

  final OrgSocial social;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final links = <(IconData, String, String?)>[
      (Icons.close, 'X', social.x),
      (Icons.camera_alt_outlined, 'Instagram', social.instagram),
      (Icons.business_center_outlined, 'LinkedIn', social.linkedin),
      (Icons.smart_display_outlined, 'YouTube', social.youtube),
      (Icons.music_note_outlined, 'TikTok', social.tiktok),
    ].where((l) => l.$3 != null && l.$3!.trim().isNotEmpty).toList();
    if (links.isEmpty) {
      return const SizedBox.shrink();
    }
    return ContactCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          ContactCardHeading(l10n.contactSocialTitle),
          const SizedBox(height: SimfTokens.space4), // gap-16
          // The frame lays the brand boxes left→right (X … TikTok); force LTR so
          // they keep that order under RTL. Spread edge-to-edge only when the
          // full five are set (the frame's layout); fewer links cluster at the
          // start so a partial set never floats to opposite edges. (At most five
          // social fields exist, so 5×48 always fits.)
          Directionality(
            textDirection: TextDirection.ltr,
            child: Row(
              mainAxisAlignment: links.length >= 5
                  ? MainAxisAlignment.spaceBetween
                  : MainAxisAlignment.start,
              children: <Widget>[
                for (var i = 0; i < links.length; i++) ...<Widget>[
                  if (links.length < 5 && i > 0)
                    const SizedBox(width: SimfTokens.space3),
                  _SocialButton(
                    icon: links[i].$1,
                    label: links[i].$2,
                    onTap: () => unawaited(
                      confirmThenLaunchExternal(context, links[i].$3!),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SocialButton extends StatelessWidget {
  const _SocialButton({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: label,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Container(
          width: 48,
          height: 48,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: SimfTokens.navy,
            borderRadius: BorderRadius.circular(SimfTokens.radius), // 8
            border: Border.all(
              color: SimfTokens.beigeBorder,
              width: SimfTokens.hairline,
            ),
          ),
          child: Icon(icon, color: Colors.white, size: 20),
        ),
      ),
    );
  }
}
