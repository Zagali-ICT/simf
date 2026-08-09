import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';

/// Shared navy rounded search field used across the Figma sub-page frames — a
/// magnifier at the inline start and (optionally) a tuning/filter glyph at the
/// inline end. Extracted (owner DRY, 2026-06-28) so notifications (758-2491),
/// speakers (908-1744), delegations, booths and the schedule all share one
/// search affordance instead of each re-declaring a private TextField.
class SimfSearchField extends StatelessWidget {
  const SimfSearchField({
    required this.hint,
    required this.onChanged,
    this.showTuningIcon = false,
    this.controller,
    super.key,
  });

  /// Placeholder text.
  final String hint;

  /// Fires on each keystroke with the current query.
  final ValueChanged<String> onChanged;

  /// Shows the tuning/filter glyph at the inline end (the notifications frame).
  final bool showTuningIcon;

  /// Optional external controller (e.g. to clear the field).
  final TextEditingController? controller;

  @override
  Widget build(BuildContext context) {
    // The visible placeholder is a separate node that disappears the moment the
    // user types, so the field itself had no accessible name — a screen reader
    // announced a bare "edit box" on every search surface (BUG-012). Attaching
    // [hint] as the field's semantics label names it; the paint is unchanged.
    return Semantics(
      label: hint,
      textField: true,
      child: TextField(
        controller: controller,
        onChanged: onChanged,
        style: const TextStyle(color: SimfTokens.surface),
        decoration: InputDecoration(
          hintText: hint,
          // Muted beige placeholder (matches the Figma search frames 908/922/758).
          hintStyle: SimfTokens.labelBeigeSm,
          prefixIcon: const SimfSvgIcon(
            AppAssets.icSearch,
            // Figma search frames (1341:3565 etc.) — 14px magnifier.
            size: SimfTokens.simfSearchFieldSizeSm,
            color: SimfTokens.beigeBorder,
          ),
          prefixIconConstraints:
              const BoxConstraints(minWidth: SimfTokens.simfSearchFieldMinWidth, minHeight: SimfTokens.simfSearchFieldMinHeight),
          suffixIcon: showTuningIcon
              ? const SimfSvgIcon(
                  AppAssets.icTuning,
                  size: SimfTokens.simfSearchFieldSizeMd,
                  color: SimfTokens.beigeBorder,
                )
              : null,
          suffixIconConstraints:
              const BoxConstraints(minWidth: SimfTokens.simfSearchFieldMinWidth, minHeight: SimfTokens.simfSearchFieldMinHeight),
          // Figma search frames (908/922/758) — an unfilled box with a 0.2px
          // beige hairline; the navy page shows through (no card-like fill).
          filled: false,
          contentPadding: const EdgeInsets.symmetric(vertical: 4),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            borderSide: const BorderSide(
              color: SimfTokens.beigeBorder,
              width: SimfTokens.hairline,
            ),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            borderSide: const BorderSide(
              color: SimfTokens.beigeBorder,
              width: SimfTokens.hairline,
            ),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            borderSide: const BorderSide(color: SimfTokens.accent),
          ),
        ),
      ),
    );
  }
}
