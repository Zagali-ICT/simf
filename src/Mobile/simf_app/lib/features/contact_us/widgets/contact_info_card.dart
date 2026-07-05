import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/organization_profile/organization_profile.dart';
import 'contact_card_chrome.dart';

/// The "معلومات التواصل" panel (frame node 1388:7711): phone / email / location
/// rows, each with a gold icon, from the shared org profile.
class ContactInfoCard extends StatelessWidget {
  const ContactInfoCard({
    required this.profile,
    required this.isArabic,
    super.key,
  });

  final OrgProfile profile;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final rows = <(IconData, String, String, bool)>[
      if (profile.contactPhone != null && profile.contactPhone!.isNotEmpty)
        (Icons.call, profile.contactPhone!, l10n.contactHotlineLabel, true),
      if (profile.contactEmail != null && profile.contactEmail!.isNotEmpty)
        (Icons.mail_outline, profile.contactEmail!, l10n.contactEmailLabel, true),
      if ((profile.locationFor(isArabic) ?? '').isNotEmpty)
        (
          Icons.location_on_outlined,
          profile.locationFor(isArabic)!,
          l10n.contactLocationLabel,
          false
        ),
    ];
    if (rows.isEmpty) {
      return const SizedBox.shrink();
    }
    return ContactCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          ContactCardHeading(l10n.contactInfoTitle),
          const SizedBox(height: SimfTokens.space4), // gap-16
          for (final (icon, value, sub, ltr) in rows)
            _InfoRow(icon: icon, value: value, sublabel: sub, valueLtr: ltr),
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({
    required this.icon,
    required this.value,
    required this.sublabel,
    required this.valueLtr,
  });

  final IconData icon;
  final String value;
  final String sublabel;
  final bool valueLtr;

  @override
  Widget build(BuildContext context) {
    return Container(
      // The frame walls each info row with a faint beige bottom divider
      // (Figma 1388:7723 — beige @25%).
      decoration: BoxDecoration(
        border: Border(
          bottom: BorderSide(
            color: SimfTokens.beigeBorder.withValues(alpha: 0.25),
            width: SimfTokens.hairline,
          ),
        ),
      ),
      padding: const EdgeInsets.all(SimfTokens.space2), // p-8
      // Icon leads (right edge under RTL), value + sub-label follow to its inline
      // end — matches Figma 1388:7711.
      child: Row(
        children: <Widget>[
          Container(
            width: 40,
            height: 40,
            alignment: Alignment.center,
            decoration: const BoxDecoration(
              color: SimfTokens.accent,
              borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
            ),
            child: Icon(icon, color: SimfTokens.navy, size: 18),
          ),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  value,
                  textAlign: TextAlign.start,
                  textDirection: valueLtr ? TextDirection.ltr : null,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: SimfTokens.textMd, // 14
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(height: SimfTokens.space2), // gap-8
                Text(
                  sublabel,
                  style: const TextStyle(
                    color: SimfTokens.beigeBorder,
                    fontSize: SimfTokens.textSm, // 12
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
