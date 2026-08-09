import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// Builds the app's [ThemeData] from [SimfTokens] + the **IBM Plex Sans Arabic**
/// type family (the `Mockup.html` app font, D-329). Widgets read colour and type
/// from `Theme.of(context)`, so the design lives in `tokens.dart` + this file.
///
/// The dark (navy) theme is the primary brand surface and carries the mockup's
/// component styling — cards (`rgba(255,255,255,0.04)` fill + a `--line-2`
/// hairline, radius 8), accent buttons (navy-on-gold), pill chips, hairline
/// dividers and a navy bottom-nav.
class SimfTheme {
  SimfTheme._();

  /// SIMF brand identity font (owner 2026-06-27): **FS Albert Arabic** — the
  /// same family the Website ships, bundled under `assets/fonts` so the app's
  /// Arabic + Latin text matches the brand identity. **Cairo** stays the glyph
  /// fallback so any code-point FS Albert lacks still renders. Set as each
  /// theme's `fontFamily` + `fontFamilyFallback` so every inherited text style
  /// picks up the pair. (Superseded the D-454 Inter/Cairo pairing.)
  static const String fontFamily = 'FSAlbertArabic';
  static const List<String> fontFamilyFallback = <String>['Cairo'];

  static AppBarTheme _appBar(Color bg, Color fg) => AppBarTheme(
        backgroundColor: bg,
        foregroundColor: fg,
        elevation: 0,
        centerTitle: true,
        titleTextStyle: TextStyle(
          fontFamily: fontFamily,
          fontFamilyFallback: fontFamilyFallback,
          color: fg,
          fontSize: SimfTokens.textLg,
          fontWeight: FontWeight.w700,
        ),
      );

  /// The gold primary button (white bold text, radius 4) — the KSA-Project
  /// design's primary action across all delivered frames (D-359).
  static FilledButtonThemeData get _accentButton => FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.accent,
          foregroundColor: SimfTokens.surface,
          minimumSize: const Size.fromHeight(SimfTokens.buttonHeight),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
          textStyle: const TextStyle(
            fontFamily: fontFamily,
            fontFamilyFallback: fontFamilyFallback,
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textMd,
          ),
        ),
      );

  static ThemeData light() {
    final theme = ThemeData(
      brightness: Brightness.light,
      useMaterial3: true,
      fontFamily: fontFamily,
      fontFamilyFallback: fontFamilyFallback,
      colorScheme: const ColorScheme.light(
        primary: SimfTokens.navy,
        secondary: SimfTokens.accent,
        onSecondary: SimfTokens.navy,
        onSurface: SimfTokens.ink,
        onSurfaceVariant: SimfTokens.inkMuted,
        error: SimfTokens.danger,
      ),
      scaffoldBackgroundColor: SimfTokens.background,
      appBarTheme: _appBar(SimfTokens.navy, SimfTokens.surface),
      filledButtonTheme: _accentButton,
      cardTheme: CardThemeData(
        color: SimfTokens.surface,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          side: const BorderSide(color: SimfTokens.lineLight),
        ),
      ),
      inputDecorationTheme: const InputDecorationTheme(
        filled: true,
        fillColor: SimfTokens.field,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
          borderSide: BorderSide.none,
        ),
        contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 14),
      ),
    );
    return theme;
  }

  static ThemeData dark() {
    final theme = ThemeData(
      brightness: Brightness.dark,
      useMaterial3: true,
      fontFamily: fontFamily,
      fontFamilyFallback: fontFamilyFallback,
      colorScheme: const ColorScheme.dark(
        primary: SimfTokens.accent,
        onPrimary: SimfTokens.navy,
        secondary: SimfTokens.accent,
        onSecondary: SimfTokens.navy,
        surface: SimfTokens.navy,
        onSurfaceVariant: SimfTokens.txtSecondary,
        outline: SimfTokens.line,
        error: SimfTokens.danger,
        onError: SimfTokens.surface,
      ),
      scaffoldBackgroundColor: SimfTokens.navy,
      appBarTheme: _appBar(SimfTokens.navy, SimfTokens.surface),
      filledButtonTheme: _accentButton,
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: SimfTokens.surface,
          minimumSize: const Size.fromHeight(SimfTokens.buttonHeight),
          side: const BorderSide(color: SimfTokens.beigeBorder),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
          // An explicit button textStyle does NOT inherit the theme's
          // fontFamily — carry the brand font (+ Arabic fallback) here, or
          // Arabic outlined-button labels render off-font / tofu (mirrors the
          // FilledButton _accentButton fix; D-549).
          textStyle: const TextStyle(
            fontFamily: fontFamily,
            fontFamilyFallback: fontFamilyFallback,
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textMd,
          ),
        ),
      ),
      cardTheme: CardThemeData(
        color: SimfTokens.surfaceTint,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          side: const BorderSide(color: SimfTokens.line2),
        ),
      ),
      chipTheme: const ChipThemeData(
        backgroundColor: SimfTokens.transparent,
        // Solid-gold selected pill chips — the KSA interests design (D-359).
        selectedColor: SimfTokens.accent,
        side: BorderSide(color: SimfTokens.line),
        shape: StadiumBorder(),
        labelStyle: TextStyle(
          color: SimfTokens.txtSecondary,
          fontSize: SimfTokens.textSm,
          fontWeight: FontWeight.w600,
        ),
        secondaryLabelStyle: TextStyle(
          color: SimfTokens.surface,
          fontSize: SimfTokens.textSm,
          fontWeight: FontWeight.w600,
        ),
        showCheckmark: false,
      ),
      dividerTheme: const DividerThemeData(
        color: SimfTokens.line2,
        thickness: 1,
        space: 1,
      ),
      bottomNavigationBarTheme: const BottomNavigationBarThemeData(
        backgroundColor: SimfTokens.navy,
        selectedItemColor: SimfTokens.accent,
        unselectedItemColor: SimfTokens.txtTertiary,
        type: BottomNavigationBarType.fixed,
        elevation: 0,
        showUnselectedLabels: true,
      ),
      listTileTheme: const ListTileThemeData(
        iconColor: SimfTokens.txtSecondary,
        textColor: SimfTokens.surface,
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: SimfTokens.surfaceTint,
        hintStyle: const TextStyle(color: SimfTokens.txtTertiary),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(color: SimfTokens.line2),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(color: SimfTokens.line2),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(color: SimfTokens.accent),
        ),
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
      ),
    );
    return theme;
  }

  /// High-contrast light theme — maximised text/background contrast for the
  /// Page 038 "high contrast" toggle. Built from the `hcLight*` tokens.
  static ThemeData highContrastLight() {
    final theme = ThemeData(
      brightness: Brightness.light,
      useMaterial3: true,
      fontFamily: fontFamily,
      fontFamilyFallback: fontFamilyFallback,
      colorScheme: const ColorScheme.light(
        primary: SimfTokens.hcLightInk,
        secondary: SimfTokens.hcLightInk,
        onSecondary: SimfTokens.hcLightSurface,
        error: SimfTokens.danger,
      ),
      scaffoldBackgroundColor: SimfTokens.hcLightSurface,
      appBarTheme: _appBar(SimfTokens.hcLightInk, SimfTokens.hcLightSurface),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.hcLightInk,
          foregroundColor: SimfTokens.hcLightSurface,
          minimumSize: const Size.fromHeight(SimfTokens.buttonHeight),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
          ),
          textStyle: const TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textMd,
          ),
        ),
      ),
      inputDecorationTheme: const InputDecorationTheme(
        filled: true,
        fillColor: SimfTokens.hcLightField,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
        ),
        contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 14),
      ),
    );
    return theme;
  }

  /// High-contrast dark theme — the dark-mode counterpart (white on black).
  static ThemeData highContrastDark() {
    final theme = ThemeData(
      brightness: Brightness.dark,
      useMaterial3: true,
      fontFamily: fontFamily,
      fontFamilyFallback: fontFamilyFallback,
      colorScheme: const ColorScheme.dark(
        primary: SimfTokens.hcDarkInk,
        secondary: SimfTokens.hcDarkInk,
        surface: SimfTokens.hcDarkSurface,
        error: SimfTokens.danger,
        onError: SimfTokens.hcDarkInk,
      ),
      scaffoldBackgroundColor: SimfTokens.hcDarkSurface,
      appBarTheme: _appBar(SimfTokens.hcDarkSurface, SimfTokens.hcDarkInk),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.hcDarkInk,
          foregroundColor: SimfTokens.hcDarkSurface,
          minimumSize: const Size.fromHeight(SimfTokens.buttonHeight),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
          ),
          textStyle: const TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textMd,
          ),
        ),
      ),
      inputDecorationTheme: const InputDecorationTheme(
        filled: true,
        fillColor: SimfTokens.hcDarkField,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
          borderSide: BorderSide(color: SimfTokens.hcDarkInk),
        ),
        contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 14),
      ),
    );
    return theme;
  }
}
