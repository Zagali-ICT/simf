import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/home/widgets/meta_line.dart';

/// The hero text overlay: the forum edition (name + theme + date range +
/// location) when the profile is loaded, otherwise the original discover copy.
class HeroOverlay extends StatelessWidget {
  const HeroOverlay({required this.l10n, required this.profile, super.key});

  final AppL10n l10n;
  final OrgProfile? profile;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final p = profile;
    final name = p?.nameFor(isArabic) ?? '';

    // No edition config yet → the original discover copy (zero regression).
    if (p == null || name.isEmpty) {
      return Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            l10n.discoverSection,
            style: SimfTokens.labelGoldBoldLg,
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            l10n.discoverBannerSubtitle,
            style: SimfTokens.labelWhiteMediumSm,
          ),
        ],
      );
    }

    final theme = p.titleFor(isArabic);
    final dates = p.eventDateRange(isArabic);
    final location = p.locationFor(isArabic);
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          name,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: SimfTokens.labelGoldBold,
        ),
        if (theme.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space1),
          Text(
            theme,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: SimfTokens.labelWhiteMediumSm,
          ),
        ],
        if (dates != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space1),
          MetaLine(icon: Icons.event_outlined, text: dates),
        ],
        if (location != null && location.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space1),
          MetaLine(icon: Icons.place_outlined, text: location),
        ],
      ],
    );
  }
}
