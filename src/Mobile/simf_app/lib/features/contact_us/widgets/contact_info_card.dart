import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/contact_us/widgets/contact_card_chrome.dart';
import 'package:simf_app/features/contact_us/widgets/info_row.dart';

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
            InfoRow(icon: icon, value: value, sublabel: sub, valueLtr: ltr),
        ],
      ),
    );
  }
}

