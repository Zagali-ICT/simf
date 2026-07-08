import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../app/widgets/simf_svg_icon.dart';
import '../../core/country_flag.dart';

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
              // Figma 1439:11927 — the website glyph is the simple stroked globe
              // (circle + meridian + equator), the same asset the auth screens use.
              iconAsset: 'assets/icons/auth_globe.svg',
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
    return SimfCard(
      radius: SimfTokens.radius, // 8
      borderWidth: 0, // borderless (Figma 1439:11891)
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Center(child: SizedBox(width: 108, height: 108, child: logo)),
            const SizedBox(height: SimfTokens.space4),
            Text(
              name,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textXxl, // 22
              ),
            ),
            if ((locationLine ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              _LocationLine(text: locationLine!.trim(), countryId: countryId),
            ],
            if ((tierPill ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              _TierPill(label: tierPill!.trim()),
            ],
            if ((standCode ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              _LinkRow(
                label: standLabel ?? '',
                value: standCode!.trim(),
                icon: Icons.place_outlined,
                onTap: onMap,
                // Stand→map row (Figma 1439:11904): navy fill, value above
                // label, value Bold-16, label Medium-12.
                background: SimfTokens.navy,
                valueOnTop: true,
                valueSize: SimfTokens.textLg,
                valueWeight: FontWeight.w700,
                labelWeight: FontWeight.w500,
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// The gold "City، Country" line with the country flag (Figma 1439:11895):
/// SemiBold-14 gold city, 20px flag, 8px gap, flag on the left (RTL).
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
              fontSize: SimfTokens.textMd, // 14
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
        if (flag != null) ...<Widget>[
          const SizedBox(width: SimfTokens.space2),
          Text(
            flag,
            textDirection: TextDirection.ltr,
            style: const TextStyle(fontSize: SimfTokens.textXl, height: 1), // 20
          ),
        ],
      ],
    );
  }
}

/// The full-width tier pill (Figma 1439:11898): beige-10% fill, beige hairline,
/// radius-8, px-20/py-8, gap-8; the 16px medal glyph at the inline start (right
/// in RTL, node 1439:11899) then the gold Bold-14 label (node 1439:11903).
class _TierPill extends StatelessWidget {
  const _TierPill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space5, // 20
        vertical: SimfTokens.space2, // 8
      ),
      decoration: BoxDecoration(
        color: SimfTokens.beigeFill10,
        borderRadius: BorderRadius.circular(SimfTokens.radius), // 8
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          const Icon(
            Icons.workspace_premium_outlined,
            size: 16,
            color: SimfTokens.accent,
          ),
          const SizedBox(width: SimfTokens.space2),
          Flexible(
            child: Text(
              label,
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: SimfTokens.accent,
                fontSize: SimfTokens.textMd, // 14
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// The "نبذة عن…" about card (Figma 1439:11931): borderless navyDeep, radius-8;
/// white Medium-16 right-aligned header, a beige hairline divider, then the
/// beige Regular-14 paragraph at line-height 1.5, right-aligned.
class _AboutCard extends StatelessWidget {
  const _AboutCard({required this.header, required this.body});

  final String header;
  final String body;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      radius: SimfTokens.radius, // 8
      borderWidth: 0,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              header,
              textAlign: TextAlign.start,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w500,
                fontSize: SimfTokens.textLg, // 16
              ),
            ),
            const SizedBox(height: SimfTokens.space3), // 12
            Container(
              height: SimfTokens.hairlineBold,
              color: SimfTokens.beigeBorder,
            ),
            const SizedBox(height: SimfTokens.space2), // 8
            Text(
              body,
              textAlign: TextAlign.start,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: SimfTokens.textMd, // 14
                height: 1.5,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// A label/value row with a beige-fill icon box on one end and a chevron on the
/// other — the shared shape of the stand-code→map row (Figma 1439:11904) and the
/// website row (1439:11917). The two differ only in fill, line order and weights,
/// passed in by the caller.
class _LinkRow extends StatelessWidget {
  const _LinkRow({
    required this.label,
    required this.value,
    required this.icon,
    required this.onTap,
    required this.background,
    required this.valueOnTop,
    required this.valueSize,
    required this.valueWeight,
    required this.labelWeight,
    this.iconAsset,
  });

  final String label;
  final String value;
  final IconData icon;

  /// Optional bundled SVG glyph (Figma-exact) rendered in the icon box instead
  /// of [icon] — e.g. the stroked globe on the website row.
  final String? iconAsset;
  final VoidCallback? onTap;

  /// The card fill (navy for the map row, navyDeep for the website row).
  final Color background;

  /// true → value above label (map row); false → label above value (website row).
  final bool valueOnTop;
  final double valueSize;
  final FontWeight valueWeight;
  final FontWeight labelWeight;

  @override
  Widget build(BuildContext context) {
    // TextAlign.start (not a hardcoded .right) so the row tracks the locale:
    // right in the Arabic design target, left when the language toggle flips to
    // English — matching the shared SimfLinkRow. Codes/URLs are Latin runs so
    // they keep reading order without a forced textDirection.
    final Widget valueText = Text(
      value,
      textAlign: TextAlign.start,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: TextStyle(
        color: SimfTokens.accent,
        fontWeight: valueWeight,
        fontSize: valueSize,
      ),
    );
    final bool hasLabel = label.isNotEmpty;
    final Widget labelText = Text(
      label,
      textAlign: TextAlign.start,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: TextStyle(
        color: SimfTokens.beigeBorder,
        fontWeight: labelWeight,
        fontSize: SimfTokens.textSm, // 12
      ),
    );
    // Guard the label (an empty one would add a blank line + an 8px gap).
    final List<Widget> lines = valueOnTop
        ? <Widget>[
            valueText,
            if (hasLabel) const SizedBox(height: SimfTokens.space2),
            if (hasLabel) labelText,
          ]
        : <Widget>[
            if (hasLabel) labelText,
            if (hasLabel) const SizedBox(height: SimfTokens.space2),
            valueText,
          ];
    return SimfCard(
      color: background,
      radius: SimfTokens.radius14, // 14
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4), // 16
        child: Row(
          children: <Widget>[
            _IconBox(icon: icon, iconAsset: iconAsset),
            const SizedBox(width: SimfTokens.space3), // 12
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                mainAxisSize: MainAxisSize.min,
                children: lines,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            // Figma 1439:11906/11919 — the GOLD thin left-chevron. Reuses the
            // same bundled ic_back.svg as the sponsor / speaker cards (the
            // iconamoon thin chevron the frame draws — not a filled triangle,
            // not auto-mirrored), so it matches the design and every other card
            // caret. (Owner 2026-07-08 — was a fixed BEIGE Material chevron.)
            const SimfSvgIcon(
              'assets/icons/ic_back.svg',
              size: 18,
              color: SimfTokens.accent,
            ),
          ],
        ),
      ),
    );
  }
}

/// The 44×44 beige-fill icon box (Figma 1439:11913 / 11926): beige-10% fill,
/// beige hairline, radius-4, with a 20px gold glyph centred. A bundled Figma
/// SVG ([iconAsset]) takes precedence over the Material [icon] when supplied.
class _IconBox extends StatelessWidget {
  const _IconBox({required this.icon, this.iconAsset});

  final IconData icon;
  final String? iconAsset;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 44,
      height: 44,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.beigeFill10,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall), // 4
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: iconAsset == null
          ? Icon(icon, size: 20, color: SimfTokens.accent)
          : SimfSvgIcon(iconAsset!, size: 20, color: SimfTokens.accent),
    );
  }
}
