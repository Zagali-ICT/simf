import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/contacts/widgets/channel_row.dart';

/// Shared read-only contact card (SIMF-FDS-014 §5.5). Renders a resolved
/// visitor card — initials avatar, name, job title, then the available channels
/// (organisation, country, email, Saudi + international mobile) as icon rows.
/// When [available] is false the subject is gone (deactivated / no profile,
/// E2E-MMC-011) and only the unavailable note is shown. The screens pass the
/// **already-localized** strings (the model picks Arabic/English); this widget
/// renders only — no API, no business rules. Final visuals come from
/// SIMF-VID-001.
class ContactCard extends StatelessWidget {
  const ContactCard({
    required this.name,
    required this.available,
    super.key,
    this.jobTitle,
    this.organisation,
    this.country,
    this.email,
    this.saudiMobile,
    this.internationalMobile,
    this.note,
  });

  final String name;
  final bool available;
  final String? jobTitle;
  final String? organisation;
  final String? country;
  final String? email;
  final String? saudiMobile;
  final String? internationalMobile;
  final String? note;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                CircleAvatar(
                  radius: SimfTokens.contactCardRadius,
                  backgroundColor: SimfTokens.accent,
                  child: Text(
                    _initials(name),
                    style: SimfTokens.labelNavyBold,
                  ),
                ),
                const SizedBox(width: SimfTokens.space3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        available ? name : l10n.contactUnavailable,
                        style: SimfTokens.titleBold,
                      ),
                      if (available && _has(jobTitle)) ...<Widget>[
                        const SizedBox(height: SimfTokens.space1),
                        Text(
                          jobTitle!,
                          style: SimfTokens.bodyInkMutedSm,
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
            if (available) ...<Widget>[
              ChannelRow(icon: Icons.business_outlined, value: organisation),
              ChannelRow(icon: Icons.public, value: country),
              ChannelRow(icon: Icons.email_outlined, value: email),
              ChannelRow(icon: Icons.phone_outlined, value: saudiMobile),
              ChannelRow(
                icon: Icons.phone_iphone_outlined,
                value: internationalMobile,
              ),
            ],
            if (_has(note)) ...<Widget>[
              const Divider(height: SimfTokens.space5),
              Text(
                l10n.contactNoteLabel,
                style: SimfTokens.labelInkMutedBoldXs,
              ),
              const SizedBox(height: SimfTokens.space1),
              Text(note!),
            ],
          ],
        ),
      ),
    );
  }

  static bool _has(String? v) => v != null && v.trim().isNotEmpty;

  static String _initials(String name) {
    final parts =
        name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) {
      return '–';
    }
    if (parts.length == 1) {
      return parts.first.characters.first.toUpperCase();
    }
    return (parts.first.characters.first + parts.last.characters.first)
        .toUpperCase();
  }
}
