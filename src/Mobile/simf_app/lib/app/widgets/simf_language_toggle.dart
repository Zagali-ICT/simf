import 'package:flutter/material.dart';

import '../localization/app_l10n.dart';
import '../theme/tokens.dart';

/// The language toggle — a 48×24 navy-deep **pill** with a gold dot and the
/// target-language code (**"EN"** when Arabic is active, **"عر"** when English
/// is active), matching Figma **1967:3661** (D-670, replaces the old gold globe
/// glyph). [onPressed] flips AR ↔ EN; the control is disabled while [busy].
/// Shared by the onboarding top bar, the in-app header cluster
/// ([SimfHeaderActions]) and the auth top controls so every screen shows one
/// toggle design.
class SimfLanguageToggle extends StatelessWidget {
  const SimfLanguageToggle({
    required this.onPressed,
    this.busy = false,
    super.key,
  });

  final VoidCallback onPressed;

  /// While true the button is disabled (a request is in flight).
  final bool busy;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final isArabic = l10n.isArabic;
    // Show the language you switch TO: Arabic active → "EN", English → "عر".
    final label = isArabic ? 'EN' : 'عر';
    const dot = SizedBox(
      width: 16,
      height: 16,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: SimfTokens.accent,
          shape: BoxShape.circle,
        ),
      ),
    );
    final text = Text(
      label,
      style: const TextStyle(
        color: Colors.white,
        fontSize: 10,
        fontWeight: FontWeight.w600,
      ),
    );
    return Semantics(
      button: true,
      label: l10n.languageToggleLabel,
      child: Tooltip(
        message: l10n.languageToggleLabel,
        child: InkWell(
          key: const ValueKey<String>('languageToggle'),
          onTap: busy ? null : onPressed,
          borderRadius: BorderRadius.circular(12),
          child: Container(
            width: 48,
            height: 24,
            padding: const EdgeInsets.all(4),
            decoration: BoxDecoration(
              color: SimfTokens.navyDeep,
              borderRadius: BorderRadius.circular(12),
            ),
            // The Figma control is a fixed LTR toggle: the gold dot sits toward
            // the active side and the target-language label opposite it,
            // regardless of the app's RTL direction.
            child: Directionality(
              textDirection: TextDirection.ltr,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: isArabic
                    ? <Widget>[dot, text]
                    : <Widget>[text, dot],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
