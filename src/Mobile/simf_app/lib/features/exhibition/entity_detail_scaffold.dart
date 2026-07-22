import 'package:flutter/material.dart';

import '../../app/theme/app_assets.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'entity_about_card.dart';
import 'entity_identity_card.dart';
import 'entity_link_row.dart';

/// The shared exhibitor / sponsor detail layout — Figma **1439:11881 "العارض"**
/// and **1439:11826 "الراعي"** (the owner: reuse one template for both). A
/// borderless `navyDeep` identity card (logo, name, city·country line, full-
/// width tier pill, optional stand-code→map row) over a "نبذة عن…" about card
/// (header + beige divider + paragraph) and a website row. Each caller resolves
/// its fields and passes them in. Pixel spec verified against Figma 1439:11881.
class EntityDetailScaffold extends StatelessWidget {
  const EntityDetailScaffold({
    required this.headerTitle,
    required this.aboutHeader,
    required this.logo,
    required this.name,
    required this.websiteLabel,
    this.locationLine,
    this.countryId,
    this.tierPill,
    this.standLabel,
    this.standCode,
    this.onMap,
    this.about,
    this.website,
    this.onWebsite,
    super.key,
  });

  /// The page header (العارض / الراعي).
  final String headerTitle;

  /// The about-section header (نبذة عن العارض / نبذة عن الراعي).
  final String aboutHeader;

  /// The website-row label (الموقع الإلكتروني).
  final String websiteLabel;

  /// The pre-built logo widget (CompanyLogo / SponsorLogo image, initials fallback).
  final Widget logo;
  final String name;

  /// The "City، Country" line (gold); null hides it.
  final String? locationLine;
  final int? countryId;

  /// The localized tier pill text ("عارض بريميوم"); null hides the pill.
  final String? tierPill;

  /// The stand-code → map row (exhibitor only). [standLabel] is the muted
  /// "موقع الجناح على الخريطة"; [standCode] the gold code; [onMap] opens the map.
  final String? standLabel;
  final String? standCode;
  final VoidCallback? onMap;

  /// The about paragraph; null hides the about card.
  final String? about;

  /// The website URL (gold) + tap; null hides the website row.
  final String? website;
  final VoidCallback? onWebsite;

  @override
  Widget build(BuildContext context) {
    return SimfPageShell(
      title: headerTitle,
      onBack: () => backOrHome(context),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space6,
        ),
        children: <Widget>[
          EntityIdentityCard(
            logo: logo,
            name: name,
            locationLine: locationLine,
            countryId: countryId,
            tierPill: tierPill,
            standLabel: standLabel,
            standCode: standCode,
            onMap: onMap,
          ),
          if ((about ?? '').trim().isNotEmpty) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            EntityAboutCard(header: aboutHeader, body: about!.trim()),
          ],
          if ((website ?? '').trim().isNotEmpty) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            EntityLinkRow(
              label: websiteLabel,
              value: website!.trim(),
              icon: Icons.public,
              // Figma 1439:11927 — the website glyph is the simple stroked globe
              // (circle + meridian + equator), the same asset the auth screens use.
              iconAsset: AppAssets.authGlobe,
              onTap: onWebsite,
              // Website row (Figma 1439:11917): navyDeep fill, label above value,
              // label Bold-12, value SemiBold-14.
              background: SimfTokens.navyDeep,
              valueOnTop: false,
              valueSize: SimfTokens.textMd,
              valueWeight: FontWeight.w600,
              labelWeight: FontWeight.w700,
            ),
          ],
        ],
      ),
    );
  }
}
