import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../core/country_flag.dart';

/// The shared exhibitor / sponsor detail layout — Figma **1439:11881 "العارض"**
/// and **1439:11826 "الراعي"** (the owner: reuse one template for both). A navy
/// identity card (logo, name, city·country line, tier pill, optional
/// stand-code→map row) over a "نبذة عن…" about card and a website row. Each
/// caller (exhibitor / sponsor) resolves its fields and passes them in.
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
    return KsaPage(
      title: headerTitle,
      onBack: () => ksaBackOrHome(context),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space6,
        ),
        children: <Widget>[
          _IdentityCard(
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
            _AboutCard(header: aboutHeader, body: about!.trim()),
          ],
          if ((website ?? '').trim().isNotEmpty) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            _LinkRow(
              label: websiteLabel,
              value: website!.trim(),
              icon: Icons.public,
              onTap: onWebsite,
            ),
          ],
        ],
      ),
    );
  }
}

class _IdentityCard extends StatelessWidget {
  const _IdentityCard({
    required this.logo,
    required this.name,
    required this.locationLine,
    required this.countryId,
    required this.tierPill,
    required this.standLabel,
    required this.standCode,
    required this.onMap,
  });

  final Widget logo;
  final String name;
  final String? locationLine;
  final int? countryId;
  final String? tierPill;
  final String? standLabel;
  final String? standCode;
  final VoidCallback? onMap;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          children: <Widget>[
            SizedBox(width: 96, height: 96, child: logo),
            const SizedBox(height: SimfTokens.space3),
            Text(
              name,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textXl,
              ),
            ),
            if ((locationLine ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space2),
              _LocationLine(text: locationLine!.trim(), countryId: countryId),
            ],
            if ((tierPill ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space3),
              _TierPill(label: tierPill!.trim()),
            ],
            if ((standCode ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              _LinkRow(
                label: standLabel ?? '',
                value: standCode!.trim(),
                icon: Icons.place_outlined,
                onTap: onMap,
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// The gold "City، Country" line with the country flag (Figma 11881).
class _LocationLine extends StatelessWidget {
  const _LocationLine({required this.text, required this.countryId});

  final String text;
  final int? countryId;

  @override
  Widget build(BuildContext context) {
    final flag = countryFlagEmoji(countryId);
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Flexible(
          child: Text(
            text,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: SimfTokens.accent,
              fontSize: SimfTokens.textMd,
            ),
          ),
        ),
        if (flag != null) ...<Widget>[
          const SizedBox(width: SimfTokens.space1),
          Text(
            flag,
            textDirection: TextDirection.ltr,
            style: const TextStyle(fontSize: SimfTokens.textMd, height: 1),
          ),
        ],
      ],
    );
  }
}

/// The bordered tier pill with a medal glyph (Figma "عارض بريميوم").
class _TierPill extends StatelessWidget {
  const _TierPill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(
            Icons.workspace_premium_outlined,
            size: 16,
            color: SimfTokens.accent,
          ),
          const SizedBox(width: SimfTokens.space2),
          Text(
            label,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}

/// The "نبذة عن…" about card: a header over the paragraph, on the navy surface.
class _AboutCard extends StatelessWidget {
  const _AboutCard({required this.header, required this.body});

  final String header;
  final String body;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              header,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w600,
                fontSize: SimfTokens.textLg,
              ),
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              body,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                height: 1.6,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// A label-over-value row with a leading icon button + a trailing chevron — the
/// shared shape of the stand-code→map row and the website row (Figma 11881).
class _LinkRow extends StatelessWidget {
  const _LinkRow({
    required this.label,
    required this.value,
    required this.icon,
    required this.onTap,
  });

  final String label;
  final String value;
  final IconData icon;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      color: SimfTokens.navy,
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space3),
        child: Row(
          children: <Widget>[
            Container(
              width: 44,
              height: 44,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: SimfTokens.navyDeep,
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                border: Border.all(
                  color: SimfTokens.beigeBorder,
                  width: SimfTokens.hairline,
                ),
              ),
              child: Icon(icon, size: 20, color: SimfTokens.accent),
            ),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: <Widget>[
                  if (label.isNotEmpty)
                    Text(
                      label,
                      style: const TextStyle(
                        color: SimfTokens.beigeBorder,
                        fontSize: SimfTokens.textXs,
                      ),
                    ),
                  Text(
                    value,
                    textDirection: TextDirection.ltr,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: SimfTokens.accent,
                      fontWeight: FontWeight.w700,
                      fontSize: SimfTokens.textMd,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Icon(
              Directionality.of(context) == TextDirection.rtl
                  ? Icons.chevron_left
                  : Icons.chevron_right,
              size: 20,
              color: SimfTokens.beigeBorder,
            ),
          ],
        ),
      ),
    );
  }
}
