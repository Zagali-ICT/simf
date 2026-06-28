import 'package:flutter/material.dart';

import '../theme/tokens.dart';
import 'simf_svg_icon.dart';

/// Shared navy rounded search field used across the Figma sub-page frames — a
/// magnifier at the inline start and (optionally) a tuning/filter glyph at the
/// inline end. Extracted (owner DRY, 2026-06-28) so notifications (758-2491),
/// speakers (908-1744), delegations, booths and the schedule all share one
/// search affordance instead of each re-declaring a private TextField.
class KsaSearchField extends StatelessWidget {
  const KsaSearchField({
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
    return TextField(
      controller: controller,
      onChanged: onChanged,
      style: const TextStyle(color: Colors.white),
      decoration: InputDecoration(
        hintText: hint,
        hintStyle:
            const TextStyle(color: Colors.white, fontSize: SimfTokens.textSm),
        prefixIcon: const SimfSvgIcon(
          'assets/icons/ic_search.svg',
          size: 18,
          color: SimfTokens.beigeBorder,
        ),
        prefixIconConstraints:
            const BoxConstraints(minWidth: 44, minHeight: 44),
        suffixIcon: showTuningIcon
            ? const SimfSvgIcon(
                'assets/icons/ic_tuning.svg',
                size: 18,
                color: SimfTokens.beigeBorder,
              )
            : null,
        suffixIconConstraints:
            const BoxConstraints(minWidth: 44, minHeight: 44),
        filled: true,
        fillColor: SimfTokens.navyDeep,
        contentPadding: const EdgeInsets.symmetric(vertical: 4),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide:
              const BorderSide(color: SimfTokens.beigeBorder, width: 0.5),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide:
              const BorderSide(color: SimfTokens.beigeBorder, width: 0.5),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(color: SimfTokens.accent, width: 1),
        ),
      ),
    );
  }
}
