import 'package:flutter/material.dart';

/// Design tokens for the SIMF app (SIMF-MAA-001 v1.2 §11).
///
/// The **colour** tokens carry the delivered KSA-Project Figma design system
/// (file PSXHhY0UVTAPSaIOf9uNKd — D-358/D-359 app redesign programme); they
/// supersede the interim `Mockup.html` placeholder palette. Spacing, radii and
/// the raw type scale are unchanged until further design frames dictate
/// otherwise. Widgets reference [SimfTokens], not literals, so any future
/// design change stays local to this file.
class SimfTokens {
  SimfTokens._();

  // Core brand colours — KSA-Project Figma variables (D-359).
  static const Color navy = Color(0xFF01132D); // "Primary- Color" — dark scaffold
  static const Color navyDeep = Color(0xFF192B41); // "BG" — boxes/cards on navy
  static const Color accent = Color(0xFFC9A84C); // "Secondary- Color" — gold
  static const Color ink = Color(0xFF1A2030);
  static const Color inkMuted = Color(0xFF5A6573);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color background = Color(0xFFE9ECEF);
  static const Color field = Color(0xFFEEF1F4);
  static const Color danger = Color(0xFFA8182A);
  static const Color success = Color(0xFF2E7D32);
  static const Color warning = Color(0xFFE8932A); // amber — "being answered" (Figma 1461:12227)

  // Moderator desk per-action colours — Figma 1461:12227 (Tailwind red-600 /
  // green-600 / amber-500): reject, answered, being-answered (on-stage).
  static const Color qReject = Color(0xFFDC2626);
  static const Color qAnswered = Color(0xFF16A34A);
  static const Color qStage = Color(0xFFF59E0B);

  // Request-status colours — الطلبات chips + card borders (Figma 1408:9760+,
  // Tailwind-500): accepted green / rejected red / cancelled grey. Pending
  // reuses [qStage] (#F59E0B). Chips render the colour at 12% fill + 20% border.
  static const Color statusAccepted = Color(0xFF22C55E);
  static const Color statusRejected = Color(0xFFEF4444);
  static const Color statusCancelled = Color(0xFF6B7280);

  // KSA-Project light-surface + auth-flow palette (D-358/D-359).
  static const Color navySurface = Color(0xFF102238); // elevated navy surface (login bg)
  static const Color navyHeader = Color(0xFF071832); // darker header block behind the forum title (Figma 1467:12565 / 1461:12565)
  static const Color chipBorderNavy = Color(0xFF2A4066); // muted navy border on unselected pills (interests grid, Figma 505:1222)
  static const Color beigeBorder = Color(0xFFC2B8A2); // "Pragraph Color" — borders + on-navy paragraph text
  static const Color beigeFill10 = Color(0x1AC2B8A2); // beige 10% — tier-pill + link-row icon-box fill (Figma rgba(194,184,162,0.1))
  static const Color beigeBorder40 = Color(0x66C2B8A2); // beige 40% — agenda timeline row divider (Figma 1310:3239 rgba(194,184,162,0.4))
  static const Color cardBeige = Color(0xFFF1ECE4); // light card surface
  static const Color goldSoft = Color(0xFFD0AC77); // secondary gold text/icons
  static const Color headlineInk = Color(0xFF111827); // headings on light surfaces
  static const Color greyText = Color(0xFF6C7278); // secondary text on light surfaces
  static const Color calendarBand = Color(0xFFE9EAEC); // #4 — light-grey agenda day-strip band
  static const Color linkNavy = Color(0xFF00245E); // inline links on light surfaces
  static const Color inputInk = Color(0xCC111827); // input text on light surfaces (#111827 at 80%)
  static const Color onGoldMuted = Color(0xFFF0F0F0); // muted text on the gold identity strip (badge 758:1469)
  static const Color timestampMuted = Color(0xFF4C555F); // notification timestamp (758:2491)
  // Per-kind notification category-icon colours (Figma 758:2491 palette). Kept
  // distinct from the semantic success/danger so the icons match the mockup's
  // decorative per-kind styling exactly.
  static const Color notifGreen = Color(0xFF13C296); // Figma "Green/green-500"
  static const Color notifCoral = Color(0xFFFF6347); // Figma "Primary/primary-500"
  static const Color codeBoxBeige = Color(0xCCFFF4DC); // pale-beige code chip fill (venue map 758:1358, #FFF4DC @ 80%)
  static const Color cardShadow = Color(0x29111827); // soft card drop-shadow (headlineInk @ ~16%)
  static const Color dayInactive = Color(0xFFC2C2C2); // calendar day with no sessions (758:1415)
  static const Color bannerScrim = Color(0xCC001030); // day-banner bottom gradient #001030 @ 80% (Figma 1310:3232 / 1064:13240)
  static const Color navInactive = Color(0xFF5E584B); // bottom-nav inactive icon (758:1476)
  static const Color chatBubbleText = Color(0xFFF0F4FF); // assistant chat-bubble text (1064:13278)
  static const Color captionText = Color(0xFFDDE4F0); // live AI-caption placeholder text (934:3613)
  // Live-broadcast player band (Figma 934:3450): the LIVE pill is a brighter
  // brick-red than the semantic [danger]; the language chip is a translucent
  // dark glassy pill; the resting play button is a translucent-white circle.
  static const Color liveRed = Color(0xFFC0392B); // LIVE badge fill (934:3609)
  static const Color scrimBlack55 = Color(0x8C000000); // language chip fill rgba(0,0,0,0.55) (934:3604)
  static const Color playScrim = Color(0x26FFFFFF); // play-circle fill rgba(255,255,255,0.15) (934:3595)

  // KSA main-shell disabled palette (W2 frames 512:1492 / 512:1780): the
  // "بطاقتي" locked card and the disabled theme tile render on these.
  static const Color navyDisabled = Color(0xFF0A1628); // disabled card fill
  static const Color navyDisabledBorder = Color(0xFF1E3A5F); // disabled card border
  static const Color navyDisabledText = Color(0xFF4A6080); // disabled label/icon

  // On-navy hairlines + muted text + light-surface hairline, straight from
  // Mockup.html (--line / --line-2 / --txt-2 / --txt-3 / --line-light). These
  // drive the dark theme's cards, dividers, borders and secondary text.
  static const Color line = Color(0x1AFFFFFF); // white 10%
  static const Color line2 = Color(0x0FFFFFFF); // white 6%
  static const Color surfaceTint = Color(0x0AFFFFFF); // white 4% (card fill)
  static const Color txtSecondary = Color(0xA6FFFFFF); // white 65%
  static const Color txtTertiary = Color(0x66FFFFFF); // white 40%
  static const Color lineLight = Color(0x140F2238); // navy 8%

  // High-contrast accessibility palette (WCAG-boosted; interim, not final
  // design — see SIMF-VID-001). Used only when the Page 038 high-contrast
  // toggle is on, via SimfTheme.highContrastLight()/highContrastDark().
  static const Color hcLightSurface = Color(0xFFFFFFFF);
  static const Color hcLightInk = Color(0xFF000000);
  static const Color hcLightField = Color(0xFFEAEAEA);
  static const Color hcDarkSurface = Color(0xFF000000);
  static const Color hcDarkInk = Color(0xFFFFFFFF);
  static const Color hcDarkField = Color(0xFF1A1A1A);

  // Spacing scale.
  static const double space1 = 4;
  static const double space2 = 8;
  static const double space3 = 12;
  static const double space4 = 16;
  static const double space5 = 20;
  static const double space6 = 24;
  static const double space8 = 32;

  // Radii.
  static const double radiusSmall = 4;
  static const double radius6 = 6; // LIVE badge (Figma 934:3609)
  static const double radius = 8;
  static const double radiusLarge = 12;
  static const double radiusLg = 16; // W2 cards / nav bar top corners
  static const double radius14 = 14; // exhibitor/sponsor link rows (Figma 1439:11904/11917)
  static const double radiusXl = 20;

  /// Ready-made [BorderRadius] for the default 4px corner (fields, cards,
  /// pills). Use this instead of re-wrapping [radiusSmall] in every widget.
  static const BorderRadius borderRadiusSmall =
      BorderRadius.all(Radius.circular(radiusSmall));

  // Hairline border weights (the KSA frames' 0.2px card hairline and the
  // 0.5px emphasised hairline).
  static const double hairline = 0.2;
  static const double hairlineBold = 0.5;

  // Type scale (Material text styles override these for actual rendering;
  // tokens here are for places that need a raw size).
  static const double textXs = 10.5;
  static const double textSm = 12;
  static const double textMd = 14;
  static const double textLg = 16;
  static const double textTitle = 18; // KSA "Sub-title 18" (Phase-0 type scale)
  static const double textXl = 20;
  static const double textXxl = 22; // exhibitor/sponsor name (Figma 1439:11894)
  static const double textHero = 28;

  // Named text styles — built incrementally per screen (§5.1). The font family
  // comes from the theme; these bundle only colour/size/weight/line-height so a
  // widget never constructs a raw TextStyle. First two land with the faq pilot
  // (the on-navy beige "Paragraph Color" body, Figma 1388:7582).
  static const TextStyle bodyBeige = TextStyle(
    color: beigeBorder,
    fontSize: textMd,
    height: 1.5,
  );
  static const TextStyle labelBeigeMedium = TextStyle(
    color: beigeBorder,
    fontSize: textMd,
    fontWeight: FontWeight.w500,
  );
}
