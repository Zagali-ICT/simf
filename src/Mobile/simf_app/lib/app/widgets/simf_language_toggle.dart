import 'package:flutter/material.dart';

import '../localization/app_l10n.dart';
import '../theme/tokens.dart';

/// The language toggle — a 48×24 navy-deep **pill** with a gold dot and the
/// target-language code: **"EN"** when Arabic is active, **"ع"** when English
/// is active. The owner asked for the compact single letter over the earlier
/// spaced "ع ر" (D-697) — a deliberate divergence from Figma **1967:3661**
/// (D-670/D-674). [onPressed] flips AR ↔ EN; the control is disabled while [busy].
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
    // Show the language you switch TO: Arabic active → "EN", English → "ع"
    // (a single compact Arabic letter, mirroring the two-letter "EN"; D-697).
    final label = isArabic ? 'EN' : 'ع';
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
        color: SimfTokens.surface,
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
            width: SimfTokens.simfLanguageToggleWidth,
            height: SimfTokens.space6,
            padding: const EdgeInsets.all(4),
            decoration: BoxDecoration(
              color: SimfTokens.navyDeep,
              borderRadius: BorderRadius.circular(12),
            ),
            // Figma 1967:3661 also has a 10% inset top shadow; it is
            // imperceptible on the navy fill and, as the pill is shared
            // app-wide, rendering it re-locks hundreds of goldens for no
            // visible gain — deliberately omitted (D-674).
            // Fixed LTR toggle: the gold dot sits on the active side and the
            // label opposite. The dot is fixed and the label scales down so it
            // never overflows the 48px pill.
            child: Directionality(
              textDirection: TextDirection.ltr,
              child: Row(
                children: isArabic
                    ? <Widget>[
                        dot,
                        Expanded(
                          child: FittedBox(
                            fit: BoxFit.scaleDown,
                            alignment: Alignment.centerRight,
                            child: text,
                          ),
                        ),
                      ]
                    : <Widget>[
                        Expanded(
                          child: FittedBox(
                            fit: BoxFit.scaleDown,
                            alignment: Alignment.centerLeft,
                            child: text,
                          ),
                        ),
                        dot,
                      ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
