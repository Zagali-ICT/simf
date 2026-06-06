import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';

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
    super.key,
    required this.name,
    required this.available,
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
                  radius: 26,
                  backgroundColor: SimfTokens.accent,
                  child: Text(
                    _initials(name),
                    style: const TextStyle(
                      color: SimfTokens.navy,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                const SizedBox(width: SimfTokens.space3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        available ? name : l10n.contactUnavailable,
                        style: const TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: SimfTokens.textLg,
                        ),
                      ),
                      if (available && _has(jobTitle)) ...<Widget>[
                        const SizedBox(height: SimfTokens.space1),
                        Text(
                          jobTitle!,
                          style: const TextStyle(
                            color: SimfTokens.inkMuted,
                            fontSize: SimfTokens.textSm,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
            if (available) ...<Widget>[
              _ChannelRow(icon: Icons.business_outlined, value: organisation),
              _ChannelRow(icon: Icons.public, value: country),
              _ChannelRow(icon: Icons.email_outlined, value: email),
              _ChannelRow(icon: Icons.phone_outlined, value: saudiMobile),
              _ChannelRow(
                icon: Icons.phone_iphone_outlined,
                value: internationalMobile,
              ),
            ],
            if (_has(note)) ...<Widget>[
              const Divider(height: SimfTokens.space5),
              Text(
                l10n.contactNoteLabel,
                style: const TextStyle(
                  color: SimfTokens.inkMuted,
                  fontSize: SimfTokens.textXs,
                  fontWeight: FontWeight.w700,
                ),
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

/// One labelled channel line (icon + value). Renders nothing when the value is
/// blank, so the card only shows the channels the subject actually exposes.
class _ChannelRow extends StatelessWidget {
  const _ChannelRow({required this.icon, required this.value});

  final IconData icon;
  final String? value;

  @override
  Widget build(BuildContext context) {
    if (value == null || value!.trim().isEmpty) {
      return const SizedBox.shrink();
    }
    return Padding(
      padding: const EdgeInsets.only(top: SimfTokens.space3),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(icon, size: 18, color: SimfTokens.accent),
          const SizedBox(width: SimfTokens.space2),
          Expanded(child: Text(value!)),
        ],
      ),
    );
  }
}
