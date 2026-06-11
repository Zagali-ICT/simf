import 'package:flutter/material.dart';

/// Placeholder design tokens for the WS3 skeleton (SIMF-MAA-001 v1.2 §11).
///
/// These values are **not** the final design system. The final tokens come
/// from SIMF-VID-001 once the external designer delivers it. The mockup
/// colours below are pulled directly from `Mockup.html` so the placeholder
/// app looks roughly like the proposal; they are NOT a brand specification.
///
/// When SIMF-VID-001 lands, swap the literal values below for the
/// designer's token values. Widgets reference [SimfTokens], not literals,
/// so the swap is local to this file.
class SimfTokens {
  SimfTokens._();

  // Colours — taken from Mockup.html for visual continuity, NOT final.
  static const Color navy = Color(0xFF0F2238);
  static const Color navyDeep = Color(0xFF1A2E47);
  static const Color accent = Color(0xFFC9A14A);
  static const Color ink = Color(0xFF1A2030);
  static const Color inkMuted = Color(0xFF5A6573);
  static const Color surface = Color(0xFFFFFFFF);
  static const Color background = Color(0xFFE9ECEF);
  static const Color field = Color(0xFFEEF1F4);
  static const Color danger = Color(0xFFA8182A);
  static const Color success = Color(0xFF2E7D32);

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
  static const double radius = 8;
  static const double radiusLarge = 12;
  static const double radiusXl = 20;

  // Type scale (Material text styles override these for actual rendering;
  // tokens here are for places that need a raw size).
  static const double textXs = 10.5;
  static const double textSm = 12;
  static const double textMd = 14;
  static const double textLg = 16;
  static const double textXl = 20;
  static const double textHero = 28;
}
