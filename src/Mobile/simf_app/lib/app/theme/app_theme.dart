import 'package:flutter/material.dart';

import 'tokens.dart';

/// Builds the app's [ThemeData] for light and dark modes from
/// [SimfTokens]. Widgets read colour and type from `Theme.of(context)` so
/// the design swap (SIMF-VID-001) is local to `tokens.dart` and this file.
class SimfTheme {
  SimfTheme._();

  static ThemeData light() {
    return ThemeData(
      brightness: Brightness.light,
      useMaterial3: true,
      colorScheme: const ColorScheme.light(
        primary: SimfTokens.navy,
        onPrimary: SimfTokens.surface,
        secondary: SimfTokens.accent,
        onSecondary: SimfTokens.navy,
        surface: SimfTokens.surface,
        onSurface: SimfTokens.ink,
        error: SimfTokens.danger,
        onError: SimfTokens.surface,
      ),
      scaffoldBackgroundColor: SimfTokens.background,
      appBarTheme: const AppBarTheme(
        backgroundColor: SimfTokens.navy,
        foregroundColor: SimfTokens.surface,
        elevation: 0,
        centerTitle: true,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.accent,
          foregroundColor: SimfTokens.navy,
          minimumSize: const Size.fromHeight(48),
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
        fillColor: SimfTokens.field,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
          borderSide: BorderSide.none,
        ),
        contentPadding:
            EdgeInsets.symmetric(horizontal: 12, vertical: 14),
      ),
    );
  }

  static ThemeData dark() {
    return ThemeData(
      brightness: Brightness.dark,
      useMaterial3: true,
      colorScheme: const ColorScheme.dark(
        primary: SimfTokens.accent,
        onPrimary: SimfTokens.navy,
        secondary: SimfTokens.accent,
        onSecondary: SimfTokens.navy,
        surface: SimfTokens.navyDeep,
        onSurface: SimfTokens.surface,
        error: SimfTokens.danger,
        onError: SimfTokens.surface,
      ),
      scaffoldBackgroundColor: SimfTokens.navy,
      appBarTheme: const AppBarTheme(
        backgroundColor: SimfTokens.navy,
        foregroundColor: SimfTokens.surface,
        elevation: 0,
        centerTitle: true,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.accent,
          foregroundColor: SimfTokens.navy,
          minimumSize: const Size.fromHeight(48),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
          ),
          textStyle: const TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textMd,
          ),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Colors.white.withValues(alpha: 0.04),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: BorderSide.none,
        ),
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
      ),
    );
  }

  /// High-contrast light theme — maximised text/background contrast for the
  /// Page 038 "high contrast" toggle. Built from the `hcLight*` tokens so the
  /// design swap stays local to `tokens.dart` and this file.
  static ThemeData highContrastLight() {
    return ThemeData(
      brightness: Brightness.light,
      useMaterial3: true,
      colorScheme: const ColorScheme.light(
        primary: SimfTokens.hcLightInk,
        onPrimary: SimfTokens.hcLightSurface,
        secondary: SimfTokens.hcLightInk,
        onSecondary: SimfTokens.hcLightSurface,
        surface: SimfTokens.hcLightSurface,
        onSurface: SimfTokens.hcLightInk,
        error: SimfTokens.danger,
        onError: SimfTokens.hcLightSurface,
      ),
      scaffoldBackgroundColor: SimfTokens.hcLightSurface,
      appBarTheme: const AppBarTheme(
        backgroundColor: SimfTokens.hcLightInk,
        foregroundColor: SimfTokens.hcLightSurface,
        elevation: 0,
        centerTitle: true,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.hcLightInk,
          foregroundColor: SimfTokens.hcLightSurface,
          minimumSize: const Size.fromHeight(48),
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
          borderSide: BorderSide(color: SimfTokens.hcLightInk),
        ),
        contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 14),
      ),
    );
  }

  /// High-contrast dark theme — the dark-mode counterpart (white on black).
  static ThemeData highContrastDark() {
    return ThemeData(
      brightness: Brightness.dark,
      useMaterial3: true,
      colorScheme: const ColorScheme.dark(
        primary: SimfTokens.hcDarkInk,
        onPrimary: SimfTokens.hcDarkSurface,
        secondary: SimfTokens.hcDarkInk,
        onSecondary: SimfTokens.hcDarkSurface,
        surface: SimfTokens.hcDarkSurface,
        onSurface: SimfTokens.hcDarkInk,
        error: SimfTokens.danger,
        onError: SimfTokens.hcDarkInk,
      ),
      scaffoldBackgroundColor: SimfTokens.hcDarkSurface,
      appBarTheme: const AppBarTheme(
        backgroundColor: SimfTokens.hcDarkSurface,
        foregroundColor: SimfTokens.hcDarkInk,
        elevation: 0,
        centerTitle: true,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: SimfTokens.hcDarkInk,
          foregroundColor: SimfTokens.hcDarkSurface,
          minimumSize: const Size.fromHeight(48),
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
  }
}
